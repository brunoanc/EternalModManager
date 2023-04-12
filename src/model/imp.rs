use adw::prelude::*;
use gtk::gio::subclass::prelude::*;
use gtk::gio::ListModel;
use gtk::glib::{self, Object, Type};
use im::Vector;
use std::sync::RwLock;
use crate::mod_data::ModData;

#[derive(Debug, Default)]
pub struct Model(pub(super) RwLock<Vector<ModData>>);

#[glib::object_subclass]
impl ObjectSubclass for Model {
    const NAME: &'static str = "Model";
    type Type = super::Model;
    type Interfaces = (ListModel,);
}

impl ObjectImpl for Model {}

impl ListModelImpl for Model {
    fn item_type(&self) -> Type {
        ModData::static_type()
    }
    fn n_items(&self) -> u32 {
        self.0.read().unwrap().len() as u32
    }
    fn item(&self, position: u32) -> Option<glib::Object> {
        self.0.read().unwrap().get(position as usize).map(|o| o.clone().upcast::<Object>())
    }
}
