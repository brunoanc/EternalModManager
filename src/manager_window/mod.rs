use std::{
    env,
    fs::{self, File},
    path::{Path, PathBuf},
    sync::mpsc,
    thread,
    time::Duration
};

use adw::{prelude::*, Application, AlertDialog, ResponseAppearance};
use gtk::{
    gdk::{Display, DragAction, FileList, Monitor},
    gio::{File as GioFile, Cancellable},
    glib::{self, clone, KeyFile, KeyFileFlags, MainContext},
    ApplicationWindow, Builder, Button, CheckButton, DropTarget,
    FileDialog, Label, ListBox, ScrolledWindow, Widget
};
use im::Vector;
use notify::RecursiveMode;
use serde_json::{Result, Value};
use zip::ZipArchive;

use crate::{advanced_window, injector, mod_data::ModData, mod_list_row::ListBoxRow, model::Model};

// Create manager window
pub fn create(app: &Application, model: &Model) -> ApplicationWindow {
    // Create builder from UI file
    let ui_src = include_str!("manager.ui");
    let builder = Builder::from_string(ui_src);

    // Get window
    let window = builder.object::<ApplicationWindow>("MainWindow").unwrap();
    window.set_application(Some(app));

    // Set window title
    let title = format!("EternalModManager v{} by PowerBall253", env!("CARGO_PKG_VERSION"));
    window.set_title(Some(&title));

    // Get screen height
    let display = Display::default().unwrap();
    let mut height = i32::MAX;

    for monitor in display.monitors().iter::<Monitor>().filter_map(|m| m.ok()) {
        let rectangle = monitor.geometry();

        if height > rectangle.height() {
            height = rectangle.height();
        }
    }

    // Reduce height of window if screen height is too low
    if height <= 800 {
        // Reduce mod list height
        let mod_list_scrolled = builder.object::<ScrolledWindow>("ModListScrolled").unwrap();
        mod_list_scrolled.set_height_request(160);

        // Reduce mod description height
        let mod_description_scrolled = builder
            .object::<ScrolledWindow>("ModDescriptionScrolled")
            .unwrap();
        mod_description_scrolled.set_min_content_height(50);
        mod_description_scrolled.set_max_content_height(50);

        // Reduce window height
        window.set_default_height(700);
        window.set_height_request(700);
    }

    // Init run mod injector button
    let injector_button = builder.object::<Button>("RunInjector").unwrap();

    injector_button.connect_clicked(clone!(@weak window => move |_| {
        // Disable parent window
        window.set_sensitive(false);

        let (tx, rx) = mpsc::channel();

        // Run injector
        thread::spawn(move || {
            tx.send(injector::run()).unwrap();
        });

        MainContext::default().spawn_local(clone!(@weak window => async move {
            if let Ok(success) = rx.recv() {
                if !success {
                    // Create error dialog
                    let err_dialog = AlertDialog::builder()
                        .heading("Couldn't find default terminal.")
                        .body("Set it using the $TERMINAL environment variable.")
                        .default_response("ok")
                        .close_response("ok")
                        .build();

                    err_dialog.add_responses(&[("ok", "_Ok")]);

                    err_dialog.choose(&window, None::<&Cancellable>, clone!(@weak window => move |_| {
                        // Re-enable parent
                        window.set_sensitive(true);
                    }));
                }
                else {
                    // Re-enable parent window
                    window.set_sensitive(true);
                }
            }
        }));
    }));

    // Init advanced options button
    let advanced_button = builder.object::<Button>("AdvancedOptions").unwrap();

    advanced_button.connect_clicked(clone!(@weak window => move |_| {
        // Disable main window
        window.set_sensitive(false);

        // Create advanced window
        let advanced_window = advanced_window::create(&window);

        // Re-enable main window on close
        advanced_window.connect_destroy(clone!(@weak window => move |_| {
            window.set_sensitive(true);
        }));

        let settings_path = crate::GAME_PATH.get().unwrap().join("EternalModInjector Settings.txt");

        // Check if settings file exists
        if !settings_path.is_file() {

            // Create warning dialog
            let warning_dialog = AlertDialog::builder()
                .heading("Mod injector settings file not found.")
                .body("The mod injector settings section will not be available until the mod injector is ran at least once.")
                .default_response("ok")
                .close_response("ok")
                .build();

            warning_dialog.add_responses(&[("ok", "_Ok")]);

            warning_dialog.choose(&window, None::<&Cancellable>, clone!(@weak advanced_window => move |_| {
                // Show advanced window
                advanced_window.present();
            }));
        }
        else {
            // Show advanced window
            advanced_window.present();
        }
    }));

    // Get listbox from builder
    let listbox = builder.object::<ListBox>("ModList").unwrap();

    // Bind listbox to model
    listbox.bind_model(Some(model), move |item| {
        ListBoxRow::new(item.downcast_ref::<ModData>().unwrap()).upcast::<Widget>()
    });

    // Init enable/disable all checkbox
    let enable_all_checkbox = builder.object::<CheckButton>("EnableAllCheckBox").unwrap();

    enable_all_checkbox.connect_toggled(clone!(@weak model => move |checkbox| {
        model.toggle_all(checkbox.is_active());
    }));

    // Set mod properties on selection
    listbox.connect_selected_rows_changed(move |listbox| {
        // Get selected mod
        let custom_row = match listbox.selected_row() {
            Some(row) => row.downcast::<ListBoxRow>().unwrap(),
            None => return
        };

        let mod_data = custom_row.row_data().unwrap();

        // Set name
        let name = builder.object::<Label>("ModName").unwrap();
        name.set_label(&mod_data.name().unwrap());

        // Set author
        let author = builder.object::<Label>("ModAuthors").unwrap();
        author.set_label(&mod_data.author().unwrap());

        // Set description
        let description = builder.object::<Label>("ModDescription").unwrap();
        description.set_label(&mod_data.description().unwrap());

        // Set version
        let version = builder.object::<Label>("ModVersion").unwrap();
        version.set_label(&mod_data.version().unwrap());

        // Set minimum mod loader version
        let min_version = builder.object::<Label>("ModMinVersion").unwrap();
        min_version.set_label(&mod_data.required_version().unwrap());

        // Set load priority
        let load_priority = builder.object::<Label>("ModLoadPriority").unwrap();
        load_priority.set_label(&mod_data.load_priority().unwrap());

        // Set online safety message
        let online_safety_message = builder.object::<Label>("ModOnlineSafety").unwrap();
        online_safety_message.set_markup(&mod_data.online_safety_message().unwrap());
    });

    // Implement drag and drop
    let drop_target = DropTarget::new(FileList::static_type(), DragAction::COPY);

    // Filter out non-files
    drop_target.connect_accept(|_, d| d.formats().contains_type(FileList::static_type()));

    // Handle drop event
    drop_target.connect_drop(|_, v, _, _| {
        // Get files
        if let Ok(files) = v.get::<FileList>() {
            // Iterate through files
            for path in files.files().iter().filter_map(|f| f.path()) {
                // Make sure file exists and is a zip
                if !path.is_file() || path.extension().unwrap() != "zip" {
                    return false;
                }

                let mods_folder = crate::GAME_PATH.get().unwrap().join("Mods");
                let new_path = mods_folder.join(path.file_name().unwrap());

                // Check if it's already in the target folder
                if path
                    .parent()
                    .unwrap_or_else(|| Path::new(""))
                    .canonicalize()
                    .unwrap()
                    == mods_folder.canonicalize().unwrap()
                {
                    return false;
                }

                // Copy file to Mods folder
                thread::spawn(move || fs::copy(&path, &new_path));
            }

            return true;
        }

        false
    });

    listbox.add_controller(drop_target);

    window
}

// Get DOOM Eternal path
pub fn get_game_path(parent_window: &ApplicationWindow, files: &[GioFile], model: &Model) {
    // Get from arguments
    if !files.is_empty() {
        let path = files[0].path().unwrap();

        if path.is_dir() && path.join("DOOMEternalx64vk.exe").is_file() {
            // Set game path
            crate::GAME_PATH.set(path).unwrap();
            save_game_path();

            // Check modding tools
            check_modding_tools(parent_window);

            // Init watcher
            init_watcher(model);
            return;
        }
    }

    // Get from config file
    let keyfile = KeyFile::new();
    let mut config_dirs = vec![glib::user_config_dir()];
    config_dirs.append(&mut glib::system_config_dirs());

    if keyfile
        .load_from_dirs("EternalModManager/config", &config_dirs, KeyFileFlags::NONE)
        .is_ok()
    {
        if let Ok(path) = keyfile.string("settings", "game-path") {
            let path_buf = PathBuf::from(path.to_string());

            if path_buf.is_dir() && path_buf.join("DOOMEternalx64vk.exe").is_file() {
                // Set game path
                crate::GAME_PATH.set(path_buf).unwrap();
                save_game_path();

                // Check modding tools
                check_modding_tools(parent_window);

                // Init watcher
                init_watcher(model);
                return;
            }
        }
    }

    // Get from current directory
    let current_directory = env::current_dir().unwrap();

    if current_directory.join("DOOMEternalx64vk.exe").is_file() {
        // Set game path
        crate::GAME_PATH.set(current_directory).unwrap();
        save_game_path();

        // Check modding tools
        check_modding_tools(parent_window);

        // Init watcher
        init_watcher(model);
        return;
    }

    // Get from user input
    // Disable parent window
    parent_window.set_sensitive(false);

    // Create dialog
    let dialog = AlertDialog::builder()
        .heading("Open the DOOM Eternal game directory.")
        .default_response("ok")
        .close_response("ok")
        .build();

    dialog.add_responses(&[("ok", "_Ok")]);

    dialog.choose(parent_window, None::<&Cancellable>, clone!(@weak parent_window, @weak model => move |_| {
        // Create file dialog to select folder
        let file_dialog = FileDialog::builder()
            .accept_label("Open")
            .title("Open the game directory")
            .build();

        file_dialog.select_folder(Some(&parent_window), None::<&Cancellable>, clone!(@strong file_dialog, @weak parent_window, @weak model => move |result| {
            // Set game path
            if let Ok(file) = result {
                let path = file.path().unwrap();

                if path.is_dir() && path.join("DOOMEternalx64vk.exe").is_file() {
                    // Set game path
                    crate::GAME_PATH.set(path).unwrap();
                    save_game_path();

                    // Check modding tools
                    check_modding_tools(&parent_window);

                    // Init watcher
                    init_watcher(&model);
                }
            }

            // Make sure game path is set now
            if crate::GAME_PATH.get().is_none() {
                // Create error dialog
                let err_dialog = AlertDialog::builder()
                    .heading("Can't find the game directory.")
                    .body("Did you select/pass the correct directory?")
                    .default_response("ok")
                    .close_response("ok")
                    .build();

                err_dialog.add_responses(&[("ok", "_Ok")]);

                err_dialog.choose(&parent_window, None::<&Cancellable>, clone!(@weak parent_window => move |_| {
                    // Exit
                    parent_window.close();
                }));
            }
            else {
                // Re-enable parent window
                parent_window.set_sensitive(true);
            }
        }));
    }));
}

// Save game path to config file
fn save_game_path() {
    // Create config file
    let keyfile = KeyFile::new();
    keyfile.set_string(
        "settings",
        "game-path",
        &crate::GAME_PATH
            .get()
            .unwrap()
            .clone()
            .into_os_string()
            .into_string()
            .unwrap()
    );

    // Get config directory
    let config_dir = glib::user_config_dir().join("EternalModManager");

    // Create directory if necessary
    if !config_dir.exists() {
        if fs::create_dir_all(&config_dir).is_err() {
            return;
        }
    }
    else if config_dir.is_file() {
        return;
    }

    if !config_dir.is_dir() {
        return;
    }

    // Save config file
    keyfile
        .save_to_file(config_dir.join("config"))
        .expect("Could not save config file");
}

#[cfg(target_os = "windows")]
// Check for the modding tools on Windows
fn check_modding_tools(parent_window: &ApplicationWindow) {
    // Check if injector batch is present
    if !crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjector.bat")
        .is_file()
    {
        // Disable parent window
        parent_window.set_sensitive(false);

        // Create error dialog
        let err_dialog = AlertDialog::builder()
            .heading("Can't find EternalModInjector.bat.")
            .body("Make sure that the modding tools are installed.")
            .default_response("ok")
            .close_response("ok")
            .build();

        err_dialog.add_responses(&[("ok", "_Ok")]);

        err_dialog.choose(parent_window, None::<&Cancellable>, clone!(@weak parent_window => move |_| {
            // Exit
            parent_window.close();
        }));
    }
}

#[cfg(target_os = "linux")]
// Check for the modding tools on Linux (and download them)
fn check_modding_tools(parent_window: &ApplicationWindow) {
    use std::io::Cursor;

    // Check if injector batch is present
    if !crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjectorShell.sh")
        .is_file()
    {
        // Disable parent window
        parent_window.set_sensitive(false);

        // Create question dialog
        let dialog = AlertDialog::builder()
            .heading("Couldn't find the modding tools.")
            .body("Do you want to download them?")
            .default_response("yes")
            .close_response("no")
            .build();

        dialog.add_responses(&[("yes", "_Yes"), ("no", "_No")]);
        dialog.set_response_appearance("yes", ResponseAppearance::Suggested);

        dialog.choose(parent_window, None::<&Cancellable>, clone!(@weak parent_window => move |result| {
            // Check user selection
            if result == "no" {
                // Exit
                parent_window.close();
                return;
            }

            // Download modding tools
            let main_context = MainContext::default();

            main_context.spawn_local(clone!(@weak parent_window => async move {
                // Get request
                let bytes = reqwest::blocking::get(
                    "https://github.com/leveste/EternalBasher/releases/latest/download/EternalModInjectorShell.zip"
                ).and_then(|r| r.bytes());

                // Check for errors
                if bytes.is_err() {
                    // Create error dialog
                    let err_dialog = AlertDialog::builder()
                        .heading("Failed to download the modding tools.")
                        .body("Make sure that you are connected to the internet.")
                        .default_response("ok")
                        .close_response("ok")
                        .build();

                    err_dialog.add_responses(&[("ok", "_Ok")]);

                    err_dialog.choose(&parent_window, None::<&Cancellable>, clone!(@weak parent_window => move |_| {
                        // Exit
                        parent_window.close();
                    }));

                    return;
                }

                let mut content =  Cursor::new(bytes.unwrap());

                // Unzip file
                if ZipArchive::new(&mut content).and_then(|mut z| z.extract(crate::GAME_PATH.get().unwrap())).is_err() {
                    // Create error dialog
                    let err_dialog = AlertDialog::builder()
                        .heading("Failed to download the modding tools.")
                        .body("Make sure that you are connected to the internet.")
                        .default_response("ok")
                        .close_response("ok")
                        .build();

                    err_dialog.add_responses(&[("ok", "_Ok")]);

                    err_dialog.choose(&parent_window, None::<&Cancellable>, clone!(@weak parent_window => move |_| {
                        // Exit
                        parent_window.close();
                    }));

                    return;
                }

                // Re-enable parent
                parent_window.set_sensitive(true);
            }));
        }));
    }
}

// Initialize the watcher on the game and mod directories
fn init_watcher(model: &Model) {
    thread::spawn(clone!(@weak model => move || {
        let (tx, rx) = mpsc::channel();

        // Create the mod directories
        let mods_dir = crate::GAME_PATH.get().unwrap().join("Mods");
        let disabled_mods_dir = crate::GAME_PATH.get().unwrap().join("DisabledMods");

        if !mods_dir.exists() {
            fs::create_dir(&mods_dir).unwrap();
        }

        if !disabled_mods_dir.exists() {
            fs::create_dir(&disabled_mods_dir).unwrap();
        }

        // Create watcher
        let mut debouncer = notify_debouncer_mini::new_debouncer(Duration::from_millis(100), tx).unwrap();

        // Watch paths
        debouncer.watcher().watch(&mods_dir, RecursiveMode::NonRecursive).unwrap();
        debouncer.watcher().watch(&disabled_mods_dir, RecursiveMode::NonRecursive).unwrap();
        let _ = debouncer.watcher().watch(&crate::GAME_PATH.get().unwrap().join("EternalModInjector Settings.txt"), RecursiveMode::NonRecursive);

        // Get mods
        let main_context = MainContext::default();

        main_context.spawn(clone!(@weak model => async move {
            get_mods(&model);
        }));

        // Listen to watcher
        for res in rx {
            if res.is_ok() {
                main_context.spawn(clone!(@weak model => async move {
                    get_mods(&model);
                }));
            }
        }
    }));
}

// Check if mod is safe for online play
fn is_mod_online_safe(mod_zip: &mut ZipArchive<File>) -> bool {
    let mut assets_info_jsons = Vec::new();
    static UNSAFE_RESOURCE_KEYWORDS: &[&str] = &["gameresources", "pvp", "shell", "warehouse"];

    // Iterate through zip's entries
    for mod_file in mod_zip.file_names() {
        let mod_file_entry = mod_file.to_lowercase();

        // Skip directories
        if mod_file_entry.ends_with('/') {
            continue;
        }

        // Skip top-level files
        if !mod_file_entry.contains('/') {
            continue;
        }

        // Allow hidden system files
        if mod_file_entry.ends_with("desktop.ini") || mod_file_entry.ends_with(".ds_store") {
            continue;
        }

        let container_name = mod_file_entry.split('/').next().unwrap();
        let mod_name = &mod_file_entry[container_name.len() + 1..];
        let sound_container_path = crate::GAME_PATH
            .get()
            .unwrap()
            .join("base")
            .join("sound")
            .join("soundbanks")
            .join("pc")
            .join(format!("{}.snd", container_name));

        // Allow sound files
        if sound_container_path.is_file() {
            continue;
        }

        // Allow streamdb mods
        if container_name == "streamdb" {
            continue;
        }

        // Save AssetsInfo JSON files to be handled later
        if mod_file_entry.starts_with("eternalmod/assetsinfo") && mod_file_entry.ends_with(".json") {
            assets_info_jsons.push(mod_file.to_owned());
        }

        // Check if mod is modifying an online-unsafe resource
        let is_modifying_unsafe_resource = UNSAFE_RESOURCE_KEYWORDS
            .iter()
            .any(|&k| container_name.starts_with(k));

        // Files with .lwo extension are unsafe
        if PathBuf::from(&mod_file_entry)
            .extension()
            .unwrap_or_default()
            .to_str()
            .unwrap()
            .contains(".lwo")
            && is_modifying_unsafe_resource
        {
            return false;
        }

        // Allow modification of everything outside of generated/decls
        if !mod_name.starts_with("generated/decls") {
            continue;
        }

        // Do not allow mods to modify non-whitelisted files in unsafe resources
        static ONLINE_SAFE_KEYWORDS: &[&str] = &[
            "/eternalmod/",
            ".tga",
            ".png",
            ".swf",
            ".bimage",
            "/advancedscreenviewshake/",
            "/audiolog/",
            "/audiologstory/",
            "/automap/",
            "/automapplayerprofile/",
            "/automapproperties/",
            "/automapsoundprofile/",
            "/env/",
            "/font/",
            "/fontfx/",
            "/fx/",
            "/gameitem/",
            "/globalfonttable/",
            "/gorebehavior/",
            "/gorecontainer/",
            "/gorewounds/",
            "/handsbobcycle/",
            "/highlightlos/",
            "/highlights/",
            "/hitconfirmationsoundsinfo/",
            "/hud/",
            "/hudelement/",
            "/lightrig/",
            "/lodgroup/",
            "/material2/",
            "/md6def/",
            "/modelasset/",
            "/particle/",
            "/particlestage/",
            "/renderlayerdefinition/",
            "/renderparm/",
            "/renderparmmeta/",
            "/renderprogflag/",
            "/ribbon2/",
            "/rumble/",
            "/soundevent/",
            "/soundpack/",
            "/soundrtpc/",
            "/soundstate/",
            "/soundswitch/",
            "/speaker/",
            "/staticimage/",
            "/swfresources/",
            "/uianchor/",
            "/uicolor/",
            "/weaponreticle/",
            "/weaponreticleswfinfo/",
            "/entitydef/light/",
            "/entitydef/fx",
            "/entitydef/",
            "/impacteffect/",
            "/uiweapon/",
            "/globalinitialwarehouse/",
            "/globalshell/",
            "/warehouseitem/",
            "/warehouseofflinecontainer/",
            "/tooltip/",
            "/livetile/",
            "/tutorialevent/",
            "/maps/game/dlc/",
            "/maps/game/dlc2/",
            "/maps/game/hub/",
            "/maps/game/shell/",
            "/maps/game/sp/",
            "/maps/game/tutorials/",
            "/decls/campaign"
        ];

        if !ONLINE_SAFE_KEYWORDS.iter().any(|&k| mod_name.contains(k)) && is_modifying_unsafe_resource {
            return false;
        }
    }

    // Don't allow injecting files into the online-unsafe resources
    for assets_info_entry in assets_info_jsons {
        let resource_name = assets_info_entry.split('/').next().unwrap();

        // Unzip and deserialize JSON
        if let Ok(assets_info_file) = mod_zip.by_name(&assets_info_entry) {
            let deserialize: Result<Value> = serde_json::from_reader(assets_info_file);

            if let Ok(assets_info) = deserialize {
                if assets_info["resources"].is_array()
                    && !assets_info["resources"].as_array().unwrap().is_empty()
                    && UNSAFE_RESOURCE_KEYWORDS
                        .iter()
                        .any(|&k| resource_name.starts_with(k))
                {
                    return false;
                }
            }
        }
    }

    true
}

// Load a mod into the list
fn load_mod_into_list(
    mod_path: PathBuf, enabled: bool, only_load_online_safe: bool, mod_list: &mut Vector<ModData>
) {
    let file_name = mod_path.file_name().unwrap().to_str().unwrap();
    let mod_data: ModData;

    // Get mod data
    if let Ok(file) = File::open(&mod_path) {
        if let Ok(mut zip_file) = ZipArchive::new(file) {
            // Check if mod is online safe
            let is_online_safe = is_mod_online_safe(&mut zip_file);

            // Read properties from EternalMod.json
            if let Ok(eternalmod_file) = zip_file.by_name("EternalMod.json") {
                let deserialize: Result<Value> = serde_json::from_reader(eternalmod_file);

                if let Ok(eternalmod) = deserialize {
                    mod_data = ModData::new(
                        file_name,
                        true,
                        enabled,
                        is_online_safe,
                        only_load_online_safe,
                        eternalmod
                    );
                }
                else {
                    mod_data = ModData::new(
                        file_name,
                        true,
                        enabled,
                        is_online_safe,
                        only_load_online_safe,
                        Value::Null
                    );
                }
            }
            else {
                mod_data = ModData::new(
                    file_name,
                    true,
                    enabled,
                    is_online_safe,
                    only_load_online_safe,
                    Value::Null
                );
            }
        }
        else {
            mod_data = ModData::new(
                file_name,
                false,
                enabled,
                false,
                only_load_online_safe,
                Value::Null
            );
        }
    }
    else {
        mod_data = ModData::new(
            file_name,
            false,
            enabled,
            false,
            only_load_online_safe,
            Value::Null
        );
    }

    // Add to list
    mod_list.push_back(mod_data);
}

// Get all mods and add them to the mod list
fn get_mods(mods_list: &Model) {
    // Buffer to store newly loaded mods
    let mut buffer_mod_list = Vector::new();

    // Check if only online safe mods should be loaded
    let settings_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjector Settings.txt");

    let only_load_online_safe = match fs::read_to_string(settings_path) {
        Ok(settings) => settings.contains(":ONLINE_SAFE=1"),
        Err(_) => false
    };

    // Get enabled mods
    for mod_file in fs::read_dir(crate::GAME_PATH.get().unwrap().join("Mods"))
        .unwrap()
        .filter_map(|f| f.ok())
    {
        load_mod_into_list(mod_file.path(), true, only_load_online_safe, &mut buffer_mod_list);
    }

    // Get disabled mods
    for mod_file in fs::read_dir(crate::GAME_PATH.get().unwrap().join("DisabledMods"))
        .unwrap()
        .filter_map(|f| f.ok())
    {
        load_mod_into_list(
            mod_file.path(),
            false,
            only_load_online_safe,
            &mut buffer_mod_list
        );
    }

    // Sort buffer mod list
    buffer_mod_list.sort_by(|a, b| {
        a.filename()
            .unwrap()
            .to_lowercase()
            .cmp(&b.filename().unwrap().to_lowercase())
    });

    // Replace mod list
    mods_list.replace(&buffer_mod_list);
}
