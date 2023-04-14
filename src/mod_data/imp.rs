use std::sync::{atomic::AtomicBool, RwLock};

use adw::prelude::*;
use gtk::glib::{self, subclass::prelude::*, ParamSpec, Properties, Value};

#[derive(Default, Properties)]
#[properties(wrapper_type = super::ModData)]
pub struct ModData {
    #[property(get, set)]
    name: RwLock<Option<String>>,
    #[property(get, set)]
    filename: RwLock<Option<String>>,
    #[property(get, set)]
    is_valid: AtomicBool,
    #[property(get, set)]
    is_enabled: AtomicBool,
    #[property(get, set)]
    is_online_safe: AtomicBool,
    #[property(get, set)]
    online_safety_message: RwLock<Option<String>>,
    #[property(get, set)]
    icon: RwLock<Option<String>>,
    #[property(get, set)]
    author: RwLock<Option<String>>,
    #[property(get, set)]
    description: RwLock<Option<String>>,
    #[property(get, set)]
    version: RwLock<Option<String>>,
    #[property(get, set)]
    load_priority: RwLock<Option<String>>,
    #[property(get, set)]
    required_version: RwLock<Option<String>>,
    #[property(get, set)]
    tooltip: RwLock<Option<String>>
}

#[glib::object_subclass]
impl ObjectSubclass for ModData {
    const NAME: &'static str = "ModData";
    type Type = super::ModData;
}

impl ObjectImpl for ModData {
    fn properties() -> &'static [ParamSpec] {
        Self::derived_properties()
    }

    fn set_property(&self, id: usize, value: &Value, pspec: &ParamSpec) {
        self.derived_set_property(id, value, pspec)
    }

    fn property(&self, id: usize, pspec: &ParamSpec) -> Value {
        self.derived_property(id, pspec)
    }
}
