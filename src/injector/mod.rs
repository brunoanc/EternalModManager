use std::process::{Command, Stdio};

#[cfg(target_os = "linux")]
// Check if program exists on Linux
fn linux_program_exists(program: &str) -> bool {
    // Use "command -v" to check
    match Command::new("sh")
        .args(["-c", &format!("command -v {}", program)])
        .stdout(Stdio::null())
        .status()
    {
        Ok(c) => c.code().unwrap_or(1) == 0,
        Err(_) => false
    }
}

#[cfg(target_os = "linux")]
// Get sensible terminal on Linux
fn get_terminal_linux() -> Option<String> {
    use std::env;

    // List of common terminals from i3-sensible-terminal
    let mut terminals: Vec<String> = vec![
        "x-terminal-emulator".into(),
        "mate-terminal".into(),
        "gnome-terminal".into(),
        "terminator".into(),
        "xfce4-terminal".into(),
        "urxvt".into(),
        "rxvt".into(),
        "termit".into(),
        "Eterm".into(),
        "aterm".into(),
        "uxterm".into(),
        "xterm".into(),
        "roxterm".into(),
        "termite".into(),
        "lxterminal".into(),
        "terminology".into(),
        "foot".into(),
        "st".into(),
        "qterminal".into(),
        "lilyterm".into(),
        "tilix".into(),
        "terminix".into(),
        "konsole".into(),
        "kitty".into(),
        "guake".into(),
        "tilda".into(),
        "alacritty".into(),
        "hyper".into(),
    ];

    // Allow user to specify their own terminal
    if let Ok(terminal) = env::var("TERMINAL") {
        terminals.insert(0, terminal);
    }

    // Get terminal
    terminals.into_iter().find(|t| linux_program_exists(t))
}

#[cfg(target_os = "linux")]
// Run mod injector on terminal window on Linux
pub fn run() -> bool {
    use std::fs::File;

    // Get sensible terminal
    let terminal = match get_terminal_linux() {
        Some(t) => t,
        None => return false
    };

    // Get argument to run commands in terminal
    let term_arg = match terminal.as_str() {
        "gnome-terminal" => "--",
        "tilda" => "-c",
        _ => "-e"
    };

    // Create file to tell the injector we're running from the manager
    let f = File::create(crate::GAME_PATH.get().unwrap().join("ETERNALMODMANAGER"));
    drop(f);

    // Get injector path
    let injector_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjectorShell.sh")
        .into_os_string()
        .into_string()
        .unwrap();

    // Run injector
    let _ = Command::new(terminal)
        .args([term_arg, &injector_path])
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .stdin(Stdio::null())
        .status();

    true
}

#[cfg(target_os = "windows")]
// Run mod injector on terminal window on Windows
pub fn run() -> bool {
    use std::os::windows::process::CommandExt;

    // Get injector path
    let injector_path = crate::GAME_PATH
        .get()
        .unwrap()
        .join("EternalModInjector.bat")
        .into_os_string()
        .into_string()
        .unwrap();

    // Run injector
    let _ = Command::new("cmd.exe")
        .raw_arg(format!("/c start cmd.exe /c \"{}\"", injector_path))
        .current_dir(crate::GAME_PATH.get().unwrap())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .stdin(Stdio::null())
        .status();

    true
}
