use std::{
    collections::HashMap,
    fs::{self, File},
    io::{BufRead, BufReader},
    path::PathBuf,
    sync::mpsc,
    thread
};

use adw::{prelude::*, AlertDialog, ResponseAppearance};
use arboard::Clipboard;
use gtk::{
    glib::{self, clone, MainContext},
    ApplicationWindow, Box, Builder, Button, CheckButton, Entry
};
use walkdir::WalkDir;

// Create advanced window
pub fn create(parent_window: &ApplicationWindow) -> ApplicationWindow {
    // Create builder from UI file
    let ui_src = include_str!("advanced.ui");
    let builder = Builder::from_string(ui_src);

    // Get window
    let window = builder.object::<ApplicationWindow>("AdvancedWindow").unwrap();
    window.set_transient_for(Some(parent_window));

    // Checkboxes map
    let checkboxes = HashMap::from([
        (
            "AUTO_LAUNCH_GAME",
            builder.object::<CheckButton>("AutoLaunchCheckbox").unwrap()
        ),
        (
            "RESET_BACKUPS",
            builder.object::<CheckButton>("ResetBackupsCheckbox").unwrap()
        ),
        (
            "AUTO_UPDATE",
            builder.object::<CheckButton>("AutoUpdateCheckbox").unwrap()
        ),
        (
            "VERBOSE",
            builder.object::<CheckButton>("VerboseCheckbox").unwrap()
        ),
        ("SLOW", builder.object::<CheckButton>("SlowCheckbox").unwrap()),
        (
            "COMPRESS_TEXTURES",
            builder.object::<CheckButton>("CompressTexturesCheckbox").unwrap()
        ),
        (
            "DISABLE_MULTITHREADING",
            builder
                .object::<CheckButton>("DisableMultithreadingCheckbox")
                .unwrap()
        ),
        (
            "ONLINE_SAFE",
            builder.object::<CheckButton>("OnlineSafeCheckbox").unwrap()
        )
    ]);

    // Game parameters text entry
    let text_entry = builder.object::<Entry>("GameParametersEntry").unwrap();

    // Injector settings box
    let injector_settings_box = builder.object::<Box>("InjectorSettingsBox").unwrap();

    // Load injector settings
    load_injector_settings(&checkboxes, &text_entry, &injector_settings_box);

    // Init open mods folder button
    let open_mods_button = builder.object::<Button>("OpenEnabled").unwrap();

    open_mods_button.connect_clicked(|_| {
        // Open mods folder
        thread::spawn(|| open::that(crate::GAME_PATH.get().unwrap().join("Mods")));
    });

    // Init open disabled mods folder button
    let open_disabled_button = builder.object::<Button>("OpenDisabled").unwrap();

    open_disabled_button.connect_clicked(|_| {
        // Open disabled mods folder
        thread::spawn(|| open::that(crate::GAME_PATH.get().unwrap().join("DisabledMods")));
    });

    // Init open game folder button
    let open_game_folder_button = builder.object::<Button>("OpenGameFolder").unwrap();

    open_game_folder_button.connect_clicked(|_| {
        // Open game folder
        thread::spawn(|| open::that(crate::GAME_PATH.get().unwrap()));
    });

    // Init restore backups button
    let restore_backups_button = builder.object::<Button>("RestoreBackups").unwrap();

    restore_backups_button.connect_clicked(clone!(@weak window => move |button| {
        let confirmation_dialog = AlertDialog::builder()
            .heading("Are you sure?")
            .body(concat!(
                "This will restore your game to vanilla state by restoring the unmodded backed up game files.\n",
                "This process might take a while depending on the speed of your disk, so please be patient.\n",
                "Are you sure you want to continue?"
            ))
            .default_response("no")
            .close_response("no")
            .build();

        confirmation_dialog.add_responses(&[("yes", "_Yes"), ("no", "_No")]);
        confirmation_dialog.set_response_appearance("yes", ResponseAppearance::Destructive);

        // WORKAROUND: AlertDialog's close response doesn't work
        confirmation_dialog.connect_destroy(|dialog| {
            dialog.emit_by_name::<()>("response", &[&dialog.close_response()]);
        });

        let signal = confirmation_dialog.connect_response(None, clone!(@weak window, @weak button => move |_, result| {
            // Check user selection
            if result == "no" {
                return;
            }

            // Disable parent window
            window.set_sensitive(false);

            // Set button label to indicate backups are being restored
            button.set_label("Restoring backups...");

            let (tx, rx) = mpsc::channel();

            // Get backups
            thread::spawn(move || {
                // Backup restored counter
                let mut backups_restored = 0_usize;

                for backup in get_backups() {
                    // Restore backup
                    if fs::copy(&backup, &backup.with_extension("")).is_ok() {
                        backups_restored += 1;
                    }
                }

                tx.send(backups_restored).unwrap();
            });

            MainContext::default().spawn_local(clone!(@weak window => async move {
                if let Ok(backups_restored) = rx.recv() {
                    // Restore label
                    button.set_label("Restore backups");

                    // Create end prompt
                    let dialog = AlertDialog::builder()
                        .heading("Done.")
                        .body(format!("{} backups were restored.", backups_restored))
                        .default_response("ok")
                        .close_response("ok")
                        .build();

                    dialog.add_responses(&[("ok", "_Ok")]);

                    // WORKAROUND: AlertDialog's close response doesn't work
                    dialog.connect_destroy(|dialog| {
                        dialog.emit_by_name::<()>("response", &[&dialog.close_response()]);
                    });

                    let signal = dialog.connect_response(None, clone!(@weak window => move |_, _| {
                        // Re-enable parent window
                        window.set_sensitive(true);
                    }));

                    // WORKAROUND: AlertDialog's close response doesn't work
                    dialog.connect_response(None, move |d, _| {
                        d.block_signal(&signal);
                    });

                    dialog.present(&window);
                }
            }));
        }));

        // WORKAROUND: AlertDialog's close response doesn't work
        confirmation_dialog.connect_response(None, move |d, _| {
            d.block_signal(&signal);
        });

        confirmation_dialog.present(&window);
    }));

    // Init reset backups button
    let reset_backups_button = builder.object::<Button>("ResetBackups").unwrap();

    reset_backups_button.connect_clicked(clone!(@weak window => move |button| {
        // Create confirmation prompt
        let confirmation_dialog = AlertDialog::builder()
            .heading("Are you sure?")
            .body(concat!(
                "This will delete your backed up game files.\n",
                "The next time mods are injected the backups will be re-created, so make sure to verify your game files after doing this.\n",
                "Are you sure you want to continue?"
            ))
            .default_response("no")
            .close_response("no")
            .build();

        confirmation_dialog.add_responses(&[("yes", "_Yes"), ("no", "_No")]);
        confirmation_dialog.set_response_appearance("yes", ResponseAppearance::Destructive);

        // WORKAROUND: AlertDialog's close response doesn't work
        confirmation_dialog.connect_destroy(|dialog| {
            dialog.emit_by_name::<()>("response", &[&dialog.close_response()]);
        });

        let signal = confirmation_dialog.connect_response(None, clone!(@weak window, @weak button => move |_, result| {
            // Check user selection
            if result == "no" {
                return;
            }

            // Disable parent window
            window.set_sensitive(false);

            // Set button label to indicate backups are being reset
            button.set_label("Resetting backups...");

            let (tx, rx) = mpsc::channel();

            // Get backups
            thread::spawn(move || {
                // Backup deleted counter
                let mut backups_deleted = 0_usize;

                for backup in get_backups() {
                    // Delete backup
                    if fs::remove_file(&backup).is_ok() {
                        backups_deleted += 1;
                    }
                }

                tx.send(backups_deleted).unwrap();
            });

            MainContext::default().spawn_local(clone!(@weak window => async move {
                if let Ok(backups_deleted) = rx.recv() {
                    // Restore label
                    button.set_label("Reset backups");

                    // Create end prompt
                    let dialog = AlertDialog::builder()
                        .heading("Done.")
                        .body(format!("{} backups were deleted.", backups_deleted))
                        .default_response("ok")
                        .close_response("ok")
                        .build();

                    dialog.add_responses(&[("ok", "_Ok")]);

                    // WORKAROUND: AlertDialog's close response doesn't work
                    dialog.connect_destroy(|dialog| {
                        dialog.emit_by_name::<()>("response", &[&dialog.close_response()]);
                    });

                    let signal = dialog.connect_response(None, clone!(@weak window => move |_, _| {
                        // Re-enable parent window
                        window.set_sensitive(true);
                    }));

                    // WORKAROUND: AlertDialog's close response doesn't work
                    dialog.connect_response(None, move |d, _| {
                        d.block_signal(&signal);
                    });

                    dialog.present(&window);
                }
            }));
        }));

        // WORKAROUND: AlertDialog's close response doesn't work
        confirmation_dialog.connect_response(None, move |d, _| {
            d.block_signal(&signal);
        });

        confirmation_dialog.present(&window);
    }));

    // Init copy template JSON button
    let copy_template_button = builder.object::<Button>("CopyTemplate").unwrap();

    copy_template_button.connect_clicked(clone!(@weak window => move |_| {
        // Copy template to clipboard
        let _ = Clipboard::new().unwrap().set_text(
            "{\n\t\"name\": \"\",\n\t\"author\": \"\",\n\t\"description\": \"\",\n\t\"version\": \"\",\n\t\"loadPriority\": 0,\n\t\"requiredVersion\": 20\n}"
        );

        // Disable parent window
        window.set_sensitive(false);

        // Create dialog
        let dialog = AlertDialog::builder()
            .heading("Done.")
            .body("EternalMod.json template has been copied to your clipboard.")
            .default_response("ok")
            .close_response("ok")
            .build();

        dialog.add_responses(&[("ok", "_Ok")]);

        // WORKAROUND: AlertDialog's close response doesn't work
        dialog.connect_destroy(|dialog| {
            dialog.emit_by_name::<()>("response", &[&dialog.close_response()]);
        });

        let signal = dialog.connect_response(None, clone!(@weak window => move |_, _| {
            // Re-enable parent window
            window.set_sensitive(true);
        }));

        // WORKAROUND: AlertDialog's close response doesn't work
        dialog.connect_response(None, move |d, _| {
            d.block_signal(&signal);
        });

        dialog.present(&window);
    }));

    // Init save injector settings button
    let save_settings_button = builder.object::<Button>("SaveSettings").unwrap();

    save_settings_button.connect_clicked(clone!(@weak window => move |_| {
        // Disable parent window
        window.set_sensitive(false);

        // Save injector settings
        let message = if save_injector_settings(&checkboxes, &text_entry) {
            "Successfully saved the new settings."
        }
        else {
            "An error happened trying to save the new settings."
        };

        // Create dialog
        let dialog = AlertDialog::builder()
            .heading(message)
            .default_response("ok")
            .close_response("ok")
            .build();

        dialog.add_responses(&[("ok", "_Ok")]);

        // WORKAROUND: AlertDialog's close response doesn't work
        dialog.connect_destroy(|dialog| {
            dialog.emit_by_name::<()>("response", &[&dialog.close_response()]);
        });

        let signal = dialog.connect_response(None, clone!(@weak window => move |_, _| {
            // Re-enable parent window
            window.set_sensitive(true);
        }));

        // WORKAROUND: AlertDialog's close response doesn't work
        dialog.connect_response(None, move |d, _| {
            d.block_signal(&signal);
        });

        dialog.present(&window);
    }));

    window
}

// Get backups in game folder
fn get_backups() -> Vec<PathBuf> {
    let mut backups = Vec::new();

    // Check if executable backup exists
    let exe_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("DOOMEternalx64vk.exe.backup");

    if exe_path.is_file() {
        // Push to backup list
        backups.push(exe_path);
    }

    // Check if packagemapspec backup exists
    let packagemapspec_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("base")
        .join("packagemapspec.json.backup");

    if packagemapspec_path.is_file() {
        // Push to backup list
        backups.push(packagemapspec_path);
    }

    // Get backups in "base" directory
    for backup in fs::read_dir(crate::GAME_PATH.get().unwrap().join("base"))
        .unwrap()
        .filter_map(|f| f.ok())
    {
        if backup
            .file_name()
            .to_str()
            .unwrap()
            .ends_with(".resources.backup")
        {
            // Push to backup list
            backups.push(backup.path());
        }
    }

    // Get backups in "base/game" directory
    for backup in WalkDir::new(crate::GAME_PATH.get().unwrap().join("base").join("game"))
        .into_iter()
        .filter_map(|f| f.ok())
    {
        if backup
            .file_name()
            .to_str()
            .unwrap()
            .ends_with(".resources.backup")
        {
            // Push to backup list
            backups.push(backup.path().to_path_buf());
        }
    }

    // Get backups in "base/sound/soundbanks/pc" directory
    for backup in fs::read_dir(
        crate::GAME_PATH
            .get()
            .unwrap()
            .join("base")
            .join("sound")
            .join("soundbanks")
            .join("pc")
    )
    .unwrap()
    .filter_map(|f| f.ok())
    {
        if backup.file_name().to_str().unwrap().ends_with(".snd.backup") {
            // Push to backup list
            backups.push(backup.path());
        }
    }

    backups
}

// Load injector settings file
fn load_injector_settings(
    checkboxes: &HashMap<&str, CheckButton>, text_entry: &Entry, injector_settings_box: &Box
) {
    // User settings (found in file)
    let mut user_settings: HashMap<String, String> = HashMap::new();

    #[cfg(target_os = "windows")]
    // Disable auto update checkbox on Windows
    checkboxes["AUTO_UPDATE"].set_sensitive(false);

    // Get injector settings path
    let injector_settings_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjector Settings.txt");

    // Open file
    let injector_settings_file = match File::open(injector_settings_path) {
        Ok(f) => f,
        Err(_) => {
            // Disable settings box
            injector_settings_box.set_sensitive(false);
            return;
        }
    };

    // Read settings line by line
    for line in BufReader::new(injector_settings_file)
        .lines()
        .filter_map(|l| l.ok())
    {
        // Get only settings
        if !line.starts_with(':') {
            continue;
        }

        // Split line into key and value
        let split_line = line.split('=').collect::<Vec<&str>>();

        if split_line.len() != 2 {
            continue;
        }

        // Add setting to map
        let key = &split_line[0][1..];

        if checkboxes.contains_key(key) || key == "GAME_PARAMETERS" {
            user_settings.insert(key.to_owned(), split_line[1].trim().to_owned());
        }
    }

    // Check needed checkboxes
    for (setting, value) in &user_settings {
        // Skip game parameters
        if setting == "GAME_PARAMETERS" {
            continue;
        }

        // Set checkbox
        checkboxes[setting.as_str()].set_active(value == "1");
    }

    // Set game parameters text entry
    text_entry.set_text(&user_settings["GAME_PARAMETERS"]);
}

// Save injector settings file
fn save_injector_settings(checkboxes: &HashMap<&str, CheckButton>, text_entry: &Entry) -> bool {
    // Vectors for storing settings
    let mut settings_file = Vec::new();
    let mut extra_settings = Vec::new();

    // Get injector settings path
    let injector_settings_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjector Settings.txt");

    // Open file
    let injector_settings_file = match File::open(&injector_settings_path) {
        Ok(f) => f,
        Err(_) => {
            return false;
        }
    };

    // Read settings line by line
    for line in BufReader::new(&injector_settings_file)
        .lines()
        .filter_map(|l| l.ok())
    {
        // Skip empty lines
        if line.is_empty() {
            continue;
        }

        // Add non-settings
        if !line.starts_with(':') {
            extra_settings.push(line);
            continue;
        }

        // Add unknown settings
        let key = &line.split('=').next().unwrap()[1..];

        if !checkboxes.contains_key(key) && key != "GAME_PARAMETERS" {
            // Add setting to list as-is
            settings_file.push(line);
        }
    }

    // Close file
    drop(injector_settings_file);

    // Add settings to list
    for (setting, checkbox) in checkboxes {
        // Get setting value
        let setting_value = if checkbox.is_active() {
            String::from("1")
        }
        else {
            String::from("0")
        };

        // Add setting
        settings_file.push(format!(":{}={}", setting, setting_value));
    }

    // Add game parameters setting
    settings_file.push(format!(":GAME_PARAMETERS={}", text_entry.text()));

    // Append extra settings
    settings_file.push(String::default());
    settings_file.append(&mut extra_settings);

    // Join settings
    let settings_string = settings_file.join("\n") + "\n";

    // Write new config file
    fs::write(&injector_settings_path, settings_string).is_ok()
}
