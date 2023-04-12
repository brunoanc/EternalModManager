use std::env;

// Set icon on Windows
fn set_icon() {
    use winres::WindowsResource;

    // Set icon
    WindowsResource::new().set_icon("resources/icon.ico").compile().unwrap();

    // Invalidate the built crate if the libs change
    println!("cargo:rerun-if-changed=resources/icon.ico");
}

// Build script
fn main() {
    match env::var("CARGO_CFG_TARGET_OS").unwrap().as_str() {
        "windows" => set_icon(),
        _ => ()
    }
}
