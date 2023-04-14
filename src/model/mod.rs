mod imp;

use adw::{prelude::*, subclass::prelude::*};
use gtk::{
    gio::ListModel,
    glib::{wrapper, Object}
};
use im::Vector;

use crate::mod_data::ModData;

wrapper! {
    pub struct Model(ObjectSubclass<imp::Model>) @implements ListModel;
}

impl Model {
    pub fn new() -> Model {
        Object::new()
    }

    pub fn append(&self, obj: &ModData) {
        let imp = self.imp();

        let index = {
            let mut data = imp.0.write().unwrap();
            data.push_back(obj.clone());
            data.len() - 1
        };

        self.items_changed(index as u32, 0, 1);
    }

    pub fn replace(&self, other: &Vector<ModData>) {
        let imp = self.imp();
        let mut borrow = imp.0.write().unwrap();

        let bef_len = borrow.len() as u32;
        borrow.clear();

        for obj in other {
            borrow.push_back(obj.clone());
        }

        let len = borrow.len() as u32;
        drop(borrow);

        self.items_changed(0, bef_len, len);
    }

    pub fn toggle_all(&self, enable: bool) {
        let imp = self.imp();
        let borrow = imp.0.write().unwrap();

        for obj in borrow.iter() {
            obj.set_is_enabled(enable);
        }
    }

    pub fn remove(&self, index: u32) {
        let imp = self.imp();
        imp.0.write().unwrap().remove(index as usize);
        self.items_changed(index, 1, 0);
    }
}

impl Default for Model {
    fn default() -> Self {
        Self::new()
    }
}
