#![windows_subsystem = "windows"]

mod advanced_window;
mod injector;
mod manager_window;
pub mod mod_data;
mod mod_list_row;
mod model;

use std::path::PathBuf;

use adw::{prelude::*, Application};
use gtk::{
    gdk::Display,
    gio::{ApplicationFlags, File},
    glib::{self, ExitCode},
    CssProvider, Window
};
use model::Model;
use once_cell::sync::OnceCell;

// DOOM Eternal game path
static GAME_PATH: OnceCell<PathBuf> = OnceCell::new();

fn main() -> ExitCode {
    // Create app
    let app = Application::new(
        Some("com.powerball253.eternalmodmanager"),
        ApplicationFlags::HANDLES_OPEN | ApplicationFlags::NON_UNIQUE
    );

    // Set app name on X11
    glib::set_application_name("com.powerball253.eternalmodmanager");
    glib::set_prgname(Some("com.powerball253.eternalmodmanager"));

    // Load css
    app.connect_startup(load_css);

    // Activate callback
    app.connect_activate(|a| {
        activate_app(a, &[]);
    });

    // Handle arguments
    app.connect_open(|a, f, _| {
        activate_app(a, f);
    });

    // Run app
    app.run()
}

// Initialize app components
fn activate_app(app: &Application, files: &[File]) {
    #[cfg(target_os = "windows")]
    // Set dark theme if needed on Windows
    set_theme_windows();

    // Set window icon on X11
    Window::set_default_icon_name("com.powerball253.eternalmodmanager");

    // Create list model
    let model = Model::new();

    // Create manager window
    let manager_window = manager_window::create(app, &model);

    // Show window
    manager_window.present();

    // Get game path
    manager_window::get_game_path(&manager_window, files, &model);
}

#[cfg(target_os = "windows")]
// Set dark theme if needed on Windows
fn set_theme_windows() {
    use adw::{ColorScheme, StyleManager};
    use windows::UI::ViewManagement::{UIColorType, UISettings};

    // Get foreground color
    let ui_settings = UISettings::new().unwrap();
    let fg = ui_settings.GetColorValue(UIColorType::Foreground).unwrap();

    // Detect dark mode
    if fg.R == 255 {
        // Prefer dark theme
        let style_manager = StyleManager::default();
        style_manager.set_color_scheme(ColorScheme::PreferDark);
    }
    else {
        // Prefer light theme
        let style_manager = StyleManager::default();
        style_manager.set_color_scheme(ColorScheme::PreferLight);
    }
}

// Load css stylesheet
fn load_css(_: &Application) {
    // Load css file to provider
    let provider = CssProvider::new();
    provider.load_from_string(include_str!("style.css"));

    // Load provider to screen
    gtk::style_context_add_provider_for_display(
        &Display::default().unwrap(),
        &provider,
        gtk::STYLE_PROVIDER_PRIORITY_APPLICATION
    );
}
