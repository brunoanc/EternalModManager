mod imp;

use gtk::{
    Widget,
    glib::{Object, wrapper}
};

use crate::mod_data::ModData;

wrapper! {
    pub struct ListBoxRow(ObjectSubclass<imp::ListBoxRow>)
        @extends Widget, gtk::ListBoxRow,
        @implements gtk::Buildable, gtk::ConstraintTarget, gtk::Accessible, gtk::Actionable;
}

impl ListBoxRow {
    pub fn new(mod_data: &ModData) -> Self {
        Object::builder().property("row-data", mod_data).build()
    }
}
