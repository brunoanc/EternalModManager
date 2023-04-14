mod imp;

use gtk::{
    glib::{wrapper, Object},
    Widget
};

use crate::mod_data::ModData;

wrapper! {
    pub struct ListBoxRow(ObjectSubclass<imp::ListBoxRow>)
        @extends Widget, gtk::ListBoxRow;
}

impl ListBoxRow {
    pub fn new(mod_data: &ModData) -> Self {
        Object::builder().property("row-data", mod_data).build()
    }
}
