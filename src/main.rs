#![windows_subsystem = "windows"]

mod mod_list_row;
mod model;
pub mod mod_data;
mod manager_window;
mod advanced_window;
mod injector;

use adw::prelude::*;
use adw::Application;
use gtk::{CssProvider, Window};
use gtk::glib::{self, ExitCode};
use gtk::gdk::Display;
use gtk::gio::ApplicationFlags;
use once_cell::sync::OnceCell;
use std::path::PathBuf;
use model::Model;

#[allow(deprecated)] // The add_provider_for_display function shouldn't be deprecated
use gtk::StyleContext;

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
        #[cfg(target_os = "windows")]
        // Set dark theme if needed on Windows
        set_system_theme_windows();

        // Set window icon on X11
        Window::set_default_icon_name("com.powerball253.eternalmodmanager");

        // Create list model
        let model = Model::new();

        // Create manager window
        let manager_window = manager_window::create(a, &model);

        // Show window
        manager_window.present();

        // Get game path
        manager_window::get_game_path(&manager_window, &[], &model);
    });

    // Handle arguments
    app.connect_open(|a, f, _| {
        #[cfg(target_os = "windows")]
        // Set dark theme if needed on Windows
        set_system_theme_windows();

        // Set window icon on X11
        Window::set_default_icon_name("com.powerball253.eternalmodmanager");

        // Create list model
        let model = Model::new();

        // Create manager window
        let manager_window = manager_window::create(a, &model);

        // Show window
        manager_window.present();

        // Get game path
        manager_window::get_game_path(&manager_window, f, &model);
    });

    // Run app
    app.run()
}

#[cfg(target_os = "windows")]
// Set dark theme if needed on Windows
fn set_system_theme_windows() {
    use adw::{StyleManager, ColorScheme};
    use windows::UI::ViewManagement::{UISettings, UIColorType};

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
    provider.load_from_data(include_str!("style.css"));

    // Load provider to screen
    #[allow(deprecated)]
    StyleContext::add_provider_for_display(
        &Display::default().unwrap(),
        &provider,
        gtk::STYLE_PROVIDER_PRIORITY_APPLICATION
    );
}
