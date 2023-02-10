using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Platform.Storage;
using EternalModManager.Classes;
using EternalModManager.ViewModels;

namespace EternalModManager.Views;

public partial class MainWindow : Window
{
    // MainWindow constructor
    public MainWindow()
    {
        // Init window components
        InitializeComponent();

#if DEBUG
        this.AttachDevTools();
#endif

        // Add opened event handler
        Opened += OpenHandler;

        // Add drag-n-drop handlers
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, FilterDrop);

        // Remove acrylic blur on light theme
        if (App.Theme.Equals(FluentThemeMode.Light))
        {
            Background = Brushes.White;
            TransparencyLevelHint = WindowTransparencyLevel.None;
            this.FindControl<Panel>("TopLevelPanel")!.Children.Remove(this.FindControl<ExperimentalAcrylicBorder>("AcrylicBorder")!);
        }

        // Check screen resolution
        var screen = Screens.ScreenFromPoint(Position)!;

        if (screen.WorkingArea.BottomRight.Y <= 800)
        {
            this.FindControl<DataGrid>("ModsList")!.Height -= 50;
            this.FindControl<TextBlock>("DescriptionTextBlock")!.Height -= 20;
            Height -= 70;
        }

        // OS-specific changes
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Increase window height by 30 pixels (titlebar height)
            Height += 30;
            ExtendClientAreaTitleBarHeightHint = 30;
        }
        else
        {
            // Remove title bar
            this.FindControl<DockPanel>("MainPanel")!.Children.Remove(this.FindControl<TextBlock>("AppTitle")!);

            // Disable acrylic blur
            TransparencyLevelHint = WindowTransparencyLevel.None;

            // Make window not maximizable
            CanResize = true;
            MinWidth = Width;
            MaxWidth = Width;
            MinHeight = Height;
            MaxHeight = Height;
        }
    }

    // Object for locking
    private readonly object _lockingObject = new();

    // Check if program exists on Linux
    private async Task<bool> LinuxProgramExistsAsync(string program)
    {
        var process = App.RunSystemCommand("sh", $"-c \"command -v {program}\"");
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    // Handle window open
    private async void OpenHandler(object? sender, EventArgs e)
    {

        // Set dark GTK theme
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                // Check if xprop is installed
                if (!(await LinuxProgramExistsAsync("xprop")))
                {
                    await MessageBox.ShowAsync(this, MessageBox.MessageType.Error,
                        "`xprop` is not installed. Install xprop from your package manager, then try again.", MessageBox.MessageButtons.Ok);
                    Environment.Exit(1);
                }

                // Run xprop
                string theme = App.Theme.Equals(FluentThemeMode.Dark) ? "dark" : "light";
                var process = App.RunSystemCommand("xprop", $"-name \"{Title}\" -f _GTK_THEME_VARIANT 8u -set _GTK_THEME_VARIANT {theme}");
                await process.WaitForExitAsync();
            }
            catch { }
        }

        // Check if the game path is set
        if (String.IsNullOrEmpty(App.GamePath))
        {
            // Inform the user
            await MessageBox.ShowAsync(this, MessageBox.MessageType.Information, "Open the DOOM Eternal game directory.", MessageBox.MessageButtons.Ok);

            // Disable main window
            var topLevelPanel = this.FindControl<Panel>("TopLevelPanel")!;
            topLevelPanel.IsEnabled = false;
            topLevelPanel.Opacity = 0.7;

            // Prompt user for game path
            var selection = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open the game directory",
                AllowMultiple = false
            });

            if (selection.Count > 0 && selection[0].TryGetUri(out var uri) && uri.IsAbsoluteUri)
            {
                App.GamePath = uri.LocalPath;

                // Re-enable main window
                topLevelPanel.Opacity = 1;
                topLevelPanel.IsEnabled = true;
            }
        }

        // Check if game path is valid
        if (String.IsNullOrEmpty(App.GamePath) || !File.Exists(Path.Join(App.GamePath, "DOOMEternalx64vk.exe")))
        {
            // Show error and exit
            await MessageBox.ShowAsync(this, MessageBox.MessageType.Error,
                "Can't find the game directory.\nDid you select/pass the correct directory?", MessageBox.MessageButtons.Ok);
            Environment.Exit(1);
        }

        // Check if modding tools are not present (Windows)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !File.Exists(Path.Join(App.GamePath, "EternalModInjector.bat")))
        {
            // Show error and exit
            await MessageBox.ShowAsync(this, MessageBox.MessageType.Error,
                "Can't find EternalModInjector.bat. Make sure that the modding tools are installed.", MessageBox.MessageButtons.Ok);
            Environment.Exit(1);
        }

        // If modding tools are not present, prompt to download them (Linux only)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !File.Exists(Path.Join(App.GamePath, "EternalModInjectorShell.sh")))
        {
            // Prompt to download modding tools
            var result = await MessageBox.ShowAsync(this, MessageBox.MessageType.Information,
                "Couldn\'t find the modding tools, do you want to download them?", MessageBox.MessageButtons.YesCancel);

            if (result != MessageBox.MessageResult.Yes)
            {
                // Exit
                Environment.Exit(1);
            }

            // Disable window
            var topLevelPanel = this.FindControl<Panel>("TopLevelPanel")!;
            topLevelPanel.IsEnabled = false;
            topLevelPanel.Opacity = 0.7;

            // Download release from GitHub
            string zipPath = Path.Join(App.GamePath, "EternalModInjectorShell.zip");

            try
            {
                // Download zip
                using var httpClient = new HttpClient();
                await using var stream = await httpClient.GetStreamAsync("https://github.com/leveste/EternalBasher/releases/latest/download/EternalModInjectorShell.zip");
                await using var fileStream = new FileStream(zipPath, FileMode.Create);
                await stream.CopyToAsync(fileStream);

                // Extract zip
                ZipFile.ExtractToDirectory(zipPath, App.GamePath, true);

                // Remove zip
                File.Delete(zipPath);
            }
            catch
            {
                await MessageBox.ShowAsync(this, MessageBox.MessageType.Error, 
                    "Failed to download modding tools.", MessageBox.MessageButtons.Ok);
                Environment.Exit(1);
            }

            // Re-enable window
            topLevelPanel.Opacity = 1;
            topLevelPanel.IsEnabled = true;
        }

        // Write config file
        await App.SaveConfigAsync();

        // Set paths
        App.InjectorSettingsPath = Path.Join(App.GamePath, "EternalModInjector Settings.txt");
        App.ModsPath = Path.Join(App.GamePath, "Mods");
        App.DisabledModsPath = Path.Join(App.GamePath, "DisabledMods");

        // Init watcher
        (DataContext as MainWindowViewModel)!.InitWatcher();
    }

    // Toggle watcher
    private void ToggleWatcher(bool enable)
    {
        (DataContext as MainWindowViewModel)!.ModsFolderWatcher!.EnableRaisingEvents = enable;
        (DataContext as MainWindowViewModel)!.DisabledModsFolderWatcher!.EnableRaisingEvents = enable; 
        (DataContext as MainWindowViewModel)!.SettingsFileWatcher!.EnableRaisingEvents = enable;
    }

    // Handle enabling/disabling mods with the checkboxes
    private void ModCheckBox_OnChecked(object? sender, RoutedEventArgs e)
    {
        string modFileName = (sender as CheckBox)!.FindAncestorOfType<DockPanel>()!.FindDescendantOfType<TextBlock>()!.Text!;
        string src = Path.Join(App.DisabledModsPath, modFileName);
        string dest = Path.Join(App.ModsPath, modFileName);

        lock (_lockingObject)
        {
            try
            {
                if (!File.Exists(src))
                {
                    return;
                }

                if (File.Exists(dest))
                {
                    (sender as CheckBox)!.IsChecked = false;
                    return;
                }

                File.Move(src, dest, false);
            }
            catch
            {
                (sender as CheckBox)!.IsChecked = false;
            }
        }
    }

    private void ModCheckBox_OnUnchecked(object? sender, RoutedEventArgs e)
    {
        string modFileName = (sender as CheckBox)!.FindAncestorOfType<DockPanel>()!.FindDescendantOfType<TextBlock>()!.Text!;
        string src = Path.Join(App.ModsPath, modFileName);
        string dest = Path.Join(App.DisabledModsPath, modFileName);

        lock (_lockingObject)
        {
            try
            {
                if (!File.Exists(src))
                {
                    return;
                }

                if (File.Exists(dest))
                {
                    (sender as CheckBox)!.IsChecked = false;
                    return;
                }

                File.Move(src, dest, false);
            }
            catch
            {
                (sender as CheckBox)!.IsChecked = true;
            }
        }
    }

    // Handle enabling/disabling ALL mods with checkbox
    private void ToggleAllCheckBox_OnChecked(object? sender, RoutedEventArgs e)
    {
        // Do not reload mods while we change all
        ToggleWatcher(false);

        // Enable all mods
        foreach (var mod in (DataContext as MainWindowViewModel)!.ModsList.Items)
        {
            if (mod.IsEnabled)
            {
                continue;
            }

            string src = Path.Join(App.DisabledModsPath, mod.FileName);
            string dest = Path.Join(App.ModsPath, mod.FileName);

            lock (_lockingObject)
            {
                try
                {
                    if (!File.Exists(src))
                    {
                        continue;
                    }

                    if (File.Exists(dest))
                    {
                        continue;
                    }

                    File.Move(src, dest, false);
                }
                catch { }
            }
        }

        // Reload mods
        lock ((DataContext as MainWindowViewModel)!.ModsList)
        {
            (DataContext as MainWindowViewModel)!.ReloadMods();
        }

        // Re-enable reloading mods
        ToggleWatcher(true);
    }

    private void ToggleAllCheckBox_OnUnchecked(object? sender, RoutedEventArgs e)
    {
        // Do not reload mods while we change all
        ToggleWatcher(false);

        // Disable all mods
        foreach (var mod in (DataContext as MainWindowViewModel)!.ModsList.Items)
        {
            if (!mod.IsEnabled)
            {
                continue;
            }

            string src = Path.Join(App.ModsPath, mod.FileName);
            string dest = Path.Join(App.DisabledModsPath, mod.FileName);

            lock (_lockingObject)
            {
                try
                {
                    if (!File.Exists(src))
                    {
                        continue;
                    }

                    if (File.Exists(dest))
                    {
                        continue;
                    }

                    File.Move(src, dest, false);
                }
                catch { }
            }
        }

        // Reload mods
        lock ((DataContext as MainWindowViewModel)!.ModsList)
        {
            (DataContext as MainWindowViewModel)!.ReloadMods();
        }

        // Re-enable reloading mods
        ToggleWatcher(true);
    }

    // Update mod info on selection
    private void ModsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Get selected item
        var selectedItem = (sender as DataGrid)!.SelectedItem as ModInfo;

        if (selectedItem == null)
        {
            return;
        }

        // Update mod info
        (DataContext as MainWindowViewModel)!.CurrentModInfo = selectedItem;
    }

    // Handler for drag-and-drop
    private void Drop(object? sender, DragEventArgs e)
    {
        // Get filepath
        if (!e.Data.Contains(DataFormats.FileNames))
        {
            return;
        }

        foreach (var filePath in e.Data.GetFileNames()!)
        {
            // Filter out bad files
            if (string.IsNullOrEmpty(filePath))
            {
                continue;
            }

            if (!File.Exists(filePath))
            {
                continue;
            }

            // Only allow .zip files
            if (!filePath.EndsWith(".zip"))
            {
                continue;
            }

            // Copy mod to "Mods" folder
            lock (_lockingObject)
            {
                try
                {
                    File.Copy(filePath, Path.Join(App.ModsPath, Path.GetFileName(filePath)), false);
                }
                catch { }
            }
        }
    }

    // Do not allow drag and drop of invalid files
    private void FilterDrop(object? sender, DragEventArgs e)
    {
        // Get filepath
        if (!e.Data.Contains(DataFormats.FileNames))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        foreach (var filePath in e.Data.GetFileNames()!)
        {
            // Filter out bad files
            if (string.IsNullOrEmpty(filePath))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            // Only allow .zip files
            if (!filePath.EndsWith(".zip"))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }
        }
    }

    // Open advanced window on button click
    private async void AdvancedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Check if settings file exists
        if (!File.Exists(App.InjectorSettingsPath))
        {
            // Show warning
            await MessageBox.ShowAsync(this, MessageBox.MessageType.Information,
                "Mod injector settings file not found.\nThe mod injector settings section will not be available until the mod injector is ran at least once.",
                MessageBox.MessageButtons.Ok);
        }

        // Open advanced window
        await AdvancedWindow.ShowWindowAsync(this);
    }

    // Run mod injector script
    private async void RunInjectorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // List of common terminals
            List<string> terminals = new()
            {
                "x-terminal-emulator", "mate-terminal", "gnome-terminal", "terminator", "xfce4-terminal", "urxvt",
                "rxvt", "termit", "Eterm", "aterm", "uxterm", "xterm", "roxterm", "termite", "lxterminal", "terminology",
                "st", "qterminal", "lilyterm", "tilix", "terminix", "konsole", "kitty", "guake", "tilda", "alacritty", "hyper"
            };

            // Allow user to specify their own terminal
            if (Environment.GetEnvironmentVariable("TERMINAL") != null)
            {
                terminals.Insert(0, Environment.GetEnvironmentVariable("TERMINAL")!);
            }

            // Get user's terminal
            bool found = false;

            foreach (var terminal in terminals)
            {
                // Check if the shell is installed
                if (await LinuxProgramExistsAsync(terminal))
                {
                    // Found the terminal
                    found = true;

                    // Disable UI
                    var topLevelPanel = this.FindControl<Panel>("TopLevelPanel")!;
                    topLevelPanel.IsEnabled = false;
                    topLevelPanel.Opacity = 0.7;

                    // Terminal argument to run command
                    // Why did you have to deprecate -e, GNOME?
                    string termArg = "-e";

                    switch (terminal)
                    {
                        case "gnome-terminal":
                            termArg = "--";
                            break;
                        case "tilda":
                            termArg = "-c";
                            break;
                    }

                    // Create file to tell the injector we're running from the manager
                    File.Create(Path.Join(App.GamePath, "ETERNALMODMANAGER"));

                    // Run injector
                    var injectorProcess = App.RunSystemCommand(terminal, $"{termArg} \"{Path.Join(App.GamePath, "EternalModInjectorShell.sh")}\"");
                    await injectorProcess.WaitForExitAsync();

                    // Re-enable window
                    topLevelPanel.Opacity = 1;
                    topLevelPanel.IsEnabled = true;

                    break;
                }
            }

            if (!found)
            {
                // Tell user to set their terminal in env
                await MessageBox.ShowAsync(this, MessageBox.MessageType.Error,
                    "Couldn't find default terminal, set it using the $TERMINAL environment variable.",
                    MessageBox.MessageButtons.Ok);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Disable UI
            var topLevelPanel = this.FindControl<Panel>("TopLevelPanel")!;
            topLevelPanel.IsEnabled = false;
            topLevelPanel.Opacity = 0.7;

            // Run injector batch file
            var injectorProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = App.GamePath,
                Arguments = $"/c {Path.Join(App.GamePath, "EternalModInjector.bat")}",
                UseShellExecute = false
            })!;

            await injectorProcess.WaitForExitAsync();

            // Re-enable window
            topLevelPanel.Opacity = 1;
            topLevelPanel.IsEnabled = true;
        }
    }
}
