use std::{cell::RefCell, fs, path::Path, thread};

use adw::{prelude::*, subclass::prelude::*};
use gtk::{
    gdk::{self, Rectangle},
    gio::{File as GioFile, MenuModel, SimpleAction, SimpleActionGroup},
    glib::{self, clone, ParamSpec, Properties, Value},
    Builder, CheckButton, EventSequenceState, FileChooserAction, FileChooserNative, FileFilter, GestureClick,
    Grid, Label, PopoverMenu, Window
};

use crate::mod_data::ModData;

#[derive(Default, Properties, Debug)]
#[properties(wrapper_type = super::ListBoxRow)]
pub struct ListBoxRow {
    #[property(get, set, construct_only)]
    row_data: RefCell<Option<ModData>>
}

#[glib::object_subclass]
impl ObjectSubclass for ListBoxRow {
    const NAME: &'static str = "ExListBoxRow";
    type ParentType = gtk::ListBoxRow;
    type Type = super::ListBoxRow;
}

impl ObjectImpl for ListBoxRow {
    fn properties() -> &'static [ParamSpec] {
        Self::derived_properties()
    }

    fn set_property(&self, id: usize, value: &Value, pspec: &ParamSpec) {
        self.derived_set_property(id, value, pspec)
    }

    fn property(&self, id: usize, pspec: &ParamSpec) -> Value {
        self.derived_property(id, pspec)
    }

    fn constructed(&self) {
        self.parent_constructed();
        let obj = self.obj();

        let item = self.row_data.borrow();
        let item = item.as_ref().cloned().unwrap();

        // Create action "install" to install mods
        let action_install = SimpleAction::new("install", None);

        action_install.connect_activate(clone!(@weak item => move |_, _| {
            // Create file dialog to select mods
            let file_dialog = FileChooserNative::new(Some("Open .zip mod files to install"), None::<&Window>, FileChooserAction::Open, Some("Open"), Some("Cancel"));

            // Add zip filter
            let zip_filter = FileFilter::new();
            zip_filter.add_suffix("zip");
            zip_filter.set_name(Some("Zip files"));
            file_dialog.add_filter(&zip_filter);

            // Connect to file dialog response
            file_dialog.connect_response(clone!(@strong file_dialog => move |_, _| {
                // Close file dialog
                file_dialog.destroy();

                // Iterate through files
                for file in file_dialog.files().iter::<GioFile>().filter_map(|f| f.ok()) {
                    let path = file.path().unwrap();

                    // Sanity check
                    if !path.is_file() {
                        continue;
                    }

                    let mods_folder = crate::GAME_PATH.get().unwrap().join("Mods");
                    let new_path = mods_folder.join(path.file_name().unwrap());

                    // Check if it's already in the target folder
                    if path.parent().unwrap_or(Path::new("")).canonicalize().unwrap() == mods_folder.canonicalize().unwrap() {
                        continue;
                    }

                    // Copy file to Mods folder
                    thread::spawn(move || fs::copy(&path, &new_path));
                }
            }));

            // Show file dialog
            file_dialog.show();
        }));

        // Create action "open" to open mod folder
        let action_open = SimpleAction::new("open", None);

        action_open.connect_activate(clone!(@weak item => move |_, _| {
            // Get mod folder
            let parent_folder = match item.is_enabled() {
                true => crate::GAME_PATH.get().unwrap().join("Mods"),
                false => crate::GAME_PATH.get().unwrap().join("DisabledMods")
            };

            // Open folder
            thread::spawn(|| open::that(parent_folder));
        }));

        // Create action "toggle" to toggle mod
        let action_toggle = SimpleAction::new("toggle", None);

        action_toggle.connect_activate(clone!(@weak item => move |_, _| {
            // Toggle mod
            item.set_is_enabled(!item.is_enabled());
        }));

        // Create action "delete" to delete mod
        let action_delete = SimpleAction::new("delete", None);

        action_delete.connect_activate(clone!(@weak item => move |_, _| {
            // Get mod folder
            let parent_folder = match item.is_enabled() {
                true => crate::GAME_PATH.get().unwrap().join("Mods"),
                false => crate::GAME_PATH.get().unwrap().join("DisabledMods")
            };

            // Get mod path
            let mod_path = parent_folder.join(item.filename().unwrap());

            // Delete mod
            thread::spawn(|| fs::remove_file(mod_path));
        }));

        // Create action group
        let actions = SimpleActionGroup::new();
        obj.insert_action_group("mod", Some(&actions));

        // Add actions to group
        actions.add_action(&action_install);
        actions.add_action(&action_open);
        actions.add_action(&action_toggle);
        actions.add_action(&action_delete);

        // Create builder from row UI file
        let row_src = include_str!("row.ui");
        let row_builder = Builder::from_string(row_src);

        // Get grid
        let grid: Grid = row_builder.object("RowGrid").unwrap();

        // Create builder from context menu UI file
        let context_menu_src = include_str!("context_menu.ui");
        let context_menu_builder = Builder::from_string(context_menu_src);

        // Get menu model
        let menu_model = context_menu_builder
            .object::<MenuModel>("ModContextMenu")
            .unwrap();

        // Create context menu
        let context_menu = PopoverMenu::from_model(Some(&menu_model));
        context_menu.set_parent(&grid);
        context_menu.set_has_arrow(false);

        // Create right click handler
        let gesture = GestureClick::new();
        gesture.set_button(gdk::BUTTON_SECONDARY);

        // Handle right click
        gesture.connect_pressed(move |gesture, _, x, y| {
            gesture.set_state(EventSequenceState::Claimed);

            // Show popup
            context_menu.set_pointing_to(Some(&Rectangle::new(x as i32, y as i32, 1, 1)));
            context_menu.popup();
        });

        obj.add_controller(gesture);

        // Get grid elements
        let check = grid.child_at(0, 0).unwrap().downcast::<CheckButton>().unwrap();
        let name = grid.child_at(1, 0).unwrap().downcast::<Label>().unwrap();
        let icon = grid.child_at(2, 0).unwrap().downcast::<Label>().unwrap();

        // Bind properties
        item.bind_property("is-enabled", &check, "active")
            .sync_create()
            .bidirectional()
            .build();
        item.bind_property("filename", &name, "label")
            .sync_create()
            .build();
        item.bind_property("icon", &icon, "label").sync_create().build();
        item.bind_property("tooltip", &grid, "tooltip-text")
            .sync_create()
            .build();

        // Move mod on checkbox click
        check.connect_toggled(move |_| {
            // Get mod paths
            let enabled_mod_path = crate::GAME_PATH
                .get()
                .unwrap()
                .join("Mods")
                .join(item.filename().unwrap());
            let disabled_mod_path = crate::GAME_PATH
                .get()
                .unwrap()
                .join("DisabledMods")
                .join(item.filename().unwrap());

            // Check if mod is enabled
            if item.is_enabled() {
                // Move to enabled folder
                let _ = fs::rename(disabled_mod_path, enabled_mod_path);
            }
            else {
                // Move to disabled folder
                let _ = fs::rename(enabled_mod_path, disabled_mod_path);
            }
        });

        // Set child
        obj.set_child(Some(&grid));
    }
}

impl WidgetImpl for ListBoxRow {}
impl ListBoxRowImpl for ListBoxRow {}
