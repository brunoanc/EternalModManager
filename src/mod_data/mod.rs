mod imp;

use gtk::glib::{wrapper, Object};
use serde_json::Value;

wrapper! {
    pub struct ModData(ObjectSubclass<imp::ModData>);
}

impl ModData {
    pub fn new(filename: &str, is_valid: bool, is_enabled: bool, is_online_safe: bool, only_load_online_safe: bool, json: Value) -> ModData {
        // Get online safety message and icon
        let (color, tooltip, icon) = if !is_valid {
            ("red", "Invalid .zip file", "✗")
        }
        else if is_online_safe {
            ("greenyellow", "This mod is safe for use in public matches.", "✓")
        }
        else if only_load_online_safe {
            ("orange", "This mod is not safe for use in public matches. It will not be loaded.", "﹗")
        }
        else if is_enabled {
            ("red", "This mod is not safe for use in public matches. Public Battlemode matches will be disabled.", "﹗")
        }
        else {
            ("red", "This mod is not safe for use in public matches.", "﹗")
        };

        // Apply colors
        let online_safety_message = format!("<span foreground='{}'>{}</span>", color, tooltip);
        let colored_icon = format!("<span foreground='{}' weight='bold'>{}</span>", color, icon);

        // Get properties from json
        let name = json["name"].as_str().unwrap_or(filename);
        let author = json["author"].as_str().unwrap_or("Unknown.");
        let description = json["description"].as_str().unwrap_or("Not specified.");
        let version = json["version"].as_str().unwrap_or("Not specified.");
        let load_priority = json["loadPriority"].as_i64().map(|i| i.to_string()).unwrap_or("Not specified.".into());
        let required_version = json["loadPriority"].as_i64().map(|i| i.to_string()).unwrap_or("Not specified.".into());

        // Create and return object
        Object::builder()
            .property("name", name)
            .property("filename", filename)
            .property("is-valid", is_valid)
            .property("is-enabled", is_enabled)
            .property("is-online-safe", is_online_safe)
            .property("online-safety-message", online_safety_message)
            .property("icon", colored_icon)
            .property("author", author)
            .property("description", description)
            .property("version", version)
            .property("load-priority", load_priority)
            .property("required-version", required_version)
            .property("tooltip", tooltip)
            .build()
    }
}
