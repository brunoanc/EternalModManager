using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using EternalModManager.Classes;
using EternalModManager.ViewModels;

namespace EternalModManager.Views
{
    public partial class AdvancedWindow : Window
    {
        // AdvancedWindow constructor
        public AdvancedWindow()
        {
            // Init window components
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            // Load injector settings into UI
            LoadInjectorSettings();

            // Remove acrylic blur on light theme
            if (App.Theme.Equals(FluentThemeMode.Light))
            {
                Background = Brushes.White;
                TransparencyLevelHint = WindowTransparencyLevel.None;
                this.FindControl<Panel>("TopLevelPanel")!.Children.Remove(this.FindControl<ExperimentalAcrylicBorder>("AcrylicBorder")!);
            }

            // OS-specific changes
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Increase window height by 30 pixels (titlebar height)
                Height += 30;
                ExtendClientAreaTitleBarHeightHint = 30;

                // Windows requires a custom titlebar due to system chrome issues
                // Remove default titlebar buttons
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            }
            else
            {
                // Remove custom titlebar for Windows
                var mainPanel = this.FindControl<DockPanel>("MainPanel")!;
                mainPanel.Children.Remove(this.FindControl<Canvas>("AdvancedTitleBar")!);

                // Disable acrylic blur
                TransparencyLevelHint = WindowTransparencyLevel.None;

                // Make window not maximizable
                CanResize = true;
                MinWidth = Width;
                MaxWidth = Width;
                MinHeight = Height;
                MaxHeight = Height;
            }

            // Add open event handler
            Opened += async (_, _) =>
            {
                // Set dark GTK theme
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    try
                    {
                        // Run xprop
                        string theme = App.Theme.Equals(FluentThemeMode.Dark) ? "dark" : "light";
                        Process process;

                        // Check if we're running on flatpak
                        if (Environment.GetEnvironmentVariable("FLATPAK_ID") != null)
                        {
                            // Use flatpak-spawn on flatpak
                            process = Process.Start(new ProcessStartInfo
                            {
                                FileName = "flatpak-spawn",
                                Arguments = $"--host xprop -name \"{Title}\" -f _GTK_THEME_VARIANT 8u -set _GTK_THEME_VARIANT {theme}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            })!;
                        }
                        else
                        {
                            process = Process.Start(new ProcessStartInfo
                            {
                                FileName = "xprop",
                                Arguments = $"-name \"{Title}\" -f _GTK_THEME_VARIANT 8u -set _GTK_THEME_VARIANT {theme}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            })!;
                        }

                        await process.WaitForExitAsync();
                    }
                    catch { }
                }
            };
        }

        // Settings map
        private readonly Dictionary<string, string> _settings = new()
        {
            { "AUTO_LAUNCH_GAME", "AutoLaunchGameCheckbox" },
            { "GAME_PARAMETERS", "GameParametersTextBox" },
            { "RESET_BACKUPS", "ResetBackupsCheckbox" },
            { "AUTO_UPDATE", "AutoUpdateCheckbox" },
            { "VERBOSE", "VerboseCheckbox" },
            { "SLOW", "SlowCheckbox" },
            { "COMPRESS_TEXTURES", "CompressTexturesCheckbox" },
            { "DISABLE_MULTITHREADING", "DisableMultithreadingCheckbox" },
            { "ONLINE_SAFE", "OnlineSafeCheckbox" },
        };

        // Load injector settings file
        private void LoadInjectorSettings()
        {
            // User settings map
            var userSettingsMap = new Dictionary<string, string>();

            // Get settings panel
            var modInjectorSettings = this.FindControl<StackPanel>("ModInjectorSettings")!;

            // Disable auto update checkbox on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                modInjectorSettings.FindControl<CheckBox>("AutoUpdateCheckbox")!.IsEnabled = false;
            }

            // Disable if settings file doesn't exist
            if (!File.Exists(App.InjectorSettingsPath))
            {
                modInjectorSettings.IsEnabled = false;
                modInjectorSettings.Opacity = 0.7;
                return;
            }

            // Read settings line by line
            try
            {
                // Read file line by line
                foreach (var line in File.ReadLines(App.InjectorSettingsPath))
                {
                    // Get only settings
                    if (!line.StartsWith(':'))
                    {
                        continue;
                    }

                    // Split setting into key and value
                    var splitLine = line.Split('=');

                    if (splitLine.Length != 2)
                    {
                        continue;
                    }

                    // Add setting to map
                    if (_settings.ContainsKey(splitLine[0][1..]))
                    {
                        userSettingsMap[splitLine[0][1..]] = splitLine[1].Trim();
                    }
                }
            }
            catch { }

            // Check the needed setting checkboxes
            foreach (var setting in userSettingsMap.Keys)
            {
                if (setting.Equals("GAME_PARAMETERS"))
                {
                    modInjectorSettings.FindControl<TextBox>(_settings[setting])!.Text = userSettingsMap[setting];
                }
                else
                {
                    modInjectorSettings.FindControl<CheckBox>(_settings[setting])!.IsChecked =
                        (userSettingsMap[setting] == "1");
                }
            }

        }

        // Close window on close button click
        private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        // Create and display advanced options window
        public static async Task ShowWindow(Window parent)
        {
            // Create and show window
            var advancedWindow = new AdvancedWindow
            {
                DataContext = new AdvancedWindowViewModel()
            };

            // Disable parent
            var parentTopLevelPanel = parent.FindControl<Panel>("TopLevelPanel")!;
            parentTopLevelPanel.IsEnabled = false;
            parentTopLevelPanel.Opacity = 0.7;

            // Show window
            await advancedWindow.ShowDialog(parent);

            // Re-enable parent
            parentTopLevelPanel.Opacity = 1;
            parentTopLevelPanel.IsEnabled = true;
        }

        // Open the indicated path
        private void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
        {
            string folderPath = "";

            switch ((sender as Button)!.Name)
            {
                case "OpenModsButton":
                    folderPath = Path.GetFullPath(App.ModsPath);
                    break;
                case "OpenDisabledButton":
                    folderPath = Path.GetFullPath(App.DisabledModsPath);
                    break;
                case "OpenGameButton":
                    folderPath = Path.GetFullPath(App.GamePath);
                    break;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }

        // Copy JSON template to clipboard
        private async void CopyTemplateButton_OnClick(object? sender, RoutedEventArgs e)
        {
            // Copy template to clipboard
            await Application.Current!.Clipboard!.SetTextAsync(
                "{\n\t\"name\": \"\",\n\t\"author\": \"\",\n\t\"description\": \"\",\n\t\"version\": \"\",\n\t\"loadPriority\": 0,\n\t\"requiredVersion\": 20\n}");

            // Show success info window
            await MessageBox.Show(this, MessageBox.MessageType.Information,
                "EternalMod.json template has been copied to your clipboard.", MessageBox.MessageButtons.Ok);
        }

        // Switch app theme
        private async void SwitchThemeButton_OnClick(object? sender, RoutedEventArgs e)
        {
            // Ask user to confirm
            var result = await MessageBox.Show(this, MessageBox.MessageType.Information,
                "This will restart the application. Continue?", MessageBox.MessageButtons.YesCancel);

            if (result != MessageBox.MessageResult.Yes)
            {
                return;
            }

            // Write config file with opposite theme
            var config = new ConfigFile
            {
                Theme = App.Theme.Equals(FluentThemeMode.Light) ? "Dark" : "Light",
                GamePath = App.GamePath
            };

            try
            {
                // Serialize config
                string configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                // Create config directory
                if (!Directory.Exists(Directory.GetParent(App.ConfigPath)!.FullName))
                {
                    Directory.CreateDirectory(Directory.GetParent(App.ConfigPath)!.FullName);
                }

                // Write config file
                await File.WriteAllTextAsync(App.ConfigPath, configJson);
            }
            catch { }

            // Restart app
            if (Environment.GetEnvironmentVariable("FLATPAK_ID") != null)
            {
                // Use flatpak-spawn on flatpak
                Process.Start(new ProcessStartInfo
                {
                    FileName = "flatpak-spawn",
                    Arguments = "--host flatpak run com.powerball253.eternalmodmanager",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
            }
            else
            {
                Process.Start(Environment.ProcessPath!);
            }

            (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.Shutdown();
        }

        // Restore backups
        private async void RestoreBackups(object? sender, RoutedEventArgs e)
        {
            // Ask for confirmation
            var result = await MessageBox.Show(this, MessageBox.MessageType.Information,
                "This will restore your game to vanilla state by restoring the unmodded backed up game files.\n" +
                "This process might take a while depending on the speed of your disk, so please be patient.\n" +
                "Are you sure you want to continue?", MessageBox.MessageButtons.YesCancel, 170);

            if (result != MessageBox.MessageResult.Yes)
            {
                return;
            }

            // Disable window
            var topLevelPanel = this.FindControl<Panel>("TopLevelPanel")!;
            topLevelPanel.IsEnabled = false;
            topLevelPanel.Opacity = 0.7;

            // Set button content to indicate backups are being restored
            var button = this.FindControl<Button>("RestoreBackupsButton")!;
            button.Content = "Restoring backups...";

            // Restore backups
            int restoredBackups = 0;

            await Task.Run(async () =>
            {
                string exePath = Path.Join(App.GamePath, "DOOMEternalx64vk.exe");
                string packageMapSpecPath = Path.Join(App.GamePath, "base", "packagemapspec.json");

                // Restore executable
                if (File.Exists(exePath + ".backup"))
                {
                    try
                    {
                        File.Copy(exePath, exePath + ".backup", true);
                        restoredBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while restoring backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Restore packagemapspec
                if (File.Exists(packageMapSpecPath + ".backup"))
                {
                    try
                    {
                        File.Copy(packageMapSpecPath, packageMapSpecPath + ".backup", true);
                        restoredBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while restoring backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Restore backups in base directory
                foreach (var backup in Directory.EnumerateFiles(Path.Join(App.GamePath, "base"), "*.resources.backup", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Copy(backup, backup[..^7], true);
                        restoredBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while restoring backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Restore backups in base/game directory
                foreach (var backup in Directory.EnumerateFiles(Path.Join(App.GamePath, "base", "game"), "*.resources.backup", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Copy(backup, backup[..^7], true);
                        restoredBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while restoring backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Restore backups in base/sound/soundpacks/pc directory
                foreach (var backup in Directory.EnumerateFiles(Path.Join(App.GamePath, "base", "sound", "soundbanks", "pc"), "*.snd.backup", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Copy(backup, backup[..^7], true);
                        restoredBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while restoring backup file.", MessageBox.MessageButtons.Ok);
                    }
                }
            });

            // Restore button content
            button.Content = "Restore backups";

            // Re-enable window
            topLevelPanel.Opacity = 1;
            topLevelPanel.IsEnabled = true;

            // Show success info window
            await MessageBox.Show(this, MessageBox.MessageType.Information, $"{restoredBackups} backups were restored.",
                MessageBox.MessageButtons.Ok);
        }

        // Delete backups
        private async void ResetBackups(object? sender, RoutedEventArgs e)
        {
            // Ask for confirmation
            var result = await MessageBox.Show(this, MessageBox.MessageType.Warning,
                "This will delete your backed up game files.\n" +
                "The next time mods are injected the backups will be re-created, so make sure to verify your game files after doing this.\n" +
                "Are you sure you want to continue?", MessageBox.MessageButtons.YesCancel, 155);

            if (result != MessageBox.MessageResult.Yes)
            {
                return;
            }

            // Disable window
            var topLevelPanel = this.FindControl<Panel>("TopLevelPanel")!;
            topLevelPanel.IsEnabled = false;
            topLevelPanel.Opacity = 0.7;

            // Set button content to indicate backups are being deleted
            var button = this.FindControl<Button>("ResetBackupsButton")!;
            button.Content = "Resetting backups...";

            // Delete backups
            int deletedBackups = 0;

            await Task.Run(async () =>
            {
                string exeBackupPath = Path.Join(App.GamePath, "DOOMEternalx64vk.exe.backup");
                string packageMapSpecBackupPath = Path.Join(App.GamePath, "base", "packagemapspec.json.backup");

                // Delete executable backup
                if (File.Exists(exeBackupPath))
                {
                    try
                    {
                        File.Delete(exeBackupPath);
                        deletedBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while deleting backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Delete packagemapspec backup
                if (File.Exists(packageMapSpecBackupPath))
                {
                    try
                    {
                        File.Delete(packageMapSpecBackupPath);
                        deletedBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while deleting backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Delete backups in base directory
                foreach (var backup in Directory.EnumerateFiles(Path.Join(App.GamePath, "base"), "*.resources.backup", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(backup);
                        deletedBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while deleting backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Delete backups in base/game directory
                foreach (var backup in Directory.EnumerateFiles(Path.Join(App.GamePath, "base", "game"), "*.resources.backup", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(backup);
                        deletedBackups++;;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while deleting backup file.", MessageBox.MessageButtons.Ok);
                    }
                }

                // Delete backups in base/sound/soundpacks/pc directory
                foreach (var backup in Directory.EnumerateFiles(Path.Join(App.GamePath, "base", "sound", "soundbanks", "pc"), "*.snd.backup", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(backup);
                        deletedBackups++;
                    }
                    catch
                    {
                        await MessageBox.Show(this, MessageBox.MessageType.Error,
                            "Error while deleting backup file.", MessageBox.MessageButtons.Ok);
                    }
                }
            });

            // Delete backup entries from injector settings file
            if (File.Exists(App.InjectorSettingsPath))
            {
                var injectorSettings = new List<string>();

                try
                {
                    // Read file line by line
                    foreach (var line in File.ReadLines(App.InjectorSettingsPath))
                    {
                        // Get only settings
                        if (line.StartsWith(':'))
                        {
                            injectorSettings.Add(line);
                        }
                    }

                    // Write new config file
                    await File.WriteAllLinesAsync(App.InjectorSettingsPath, injectorSettings);
                }
                catch
                {
                    await MessageBox.Show(this, MessageBox.MessageType.Error,
                        "Error while removing backup files from EternalModInjector Settings.txt.", MessageBox.MessageButtons.Ok);
                }
            }

            // Restore button content
            button.Content = "Reset backups";

            // Re-enable window
            topLevelPanel.Opacity = 1;
            topLevelPanel.IsEnabled = true;

            // Show success info window
            await MessageBox.Show(this, MessageBox.MessageType.Information, $"{deletedBackups} backups were deleted.",
                MessageBox.MessageButtons.Ok);
        }

        // Save injector settings
        private async void SaveInjectorSettings(object? sender, RoutedEventArgs e)
        {
            // Get settings panel
            var modInjectorSettings = this.FindControl<StackPanel>("ModInjectorSettings")!;

            // Lists for storing settings
            var settingsFile = new List<string>();
            var extraSettings = new List<string>();

            try
            {
                // Read extra settings from file
                if (File.Exists(App.InjectorSettingsPath))
                {
                    // Read file line by line
                    foreach (var line in File.ReadLines(App.InjectorSettingsPath))
                    {
                        // Skip blank lines
                        if (String.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        // Add non-settings
                        if (!line.StartsWith(':'))
                        {
                            extraSettings.Add(line);
                            continue;
                        }

                        // Add unknown settings
                        if (!_settings.ContainsKey(line.Split('=')[0][1..]))
                        {
                            // Add setting to list as-is
                            settingsFile.Add(line);
                        }
                    }

                }
            }
            catch
            {
                // Show error
                await MessageBox.Show(this, MessageBox.MessageType.Error,
                    "An error happened while saving the new settings.", MessageBox.MessageButtons.Ok);
                return;
            }

            // Add settings to list
            foreach (var setting in _settings)
            {
                // Get setting value
                string settingValue = "";

                if (setting.Key.Equals("GAME_PARAMETERS"))
                {
                    settingValue = modInjectorSettings.FindControl<TextBox>(_settings[setting.Key])!.Text!;
                }
                else
                {
                    settingValue = modInjectorSettings.FindControl<CheckBox>(_settings[setting.Key])!.IsChecked!.Value ? "1" : "0";
                }

                // Add setting
                settingsFile.Add($":{setting.Key}={settingValue}");
            }

            // Append extra settings
            settingsFile.Add("");
            settingsFile.AddRange(extraSettings);

            try
            {
                // Write new config file
                await File.WriteAllLinesAsync(App.InjectorSettingsPath, settingsFile);
            }
            catch
            {
                // Show error
                await MessageBox.Show(this, MessageBox.MessageType.Error,
                    "An error happened while saving the new settings.", MessageBox.MessageButtons.Ok);
                return;
            }

            // Show success window
            await MessageBox.Show(this, MessageBox.MessageType.Information,
                "Successfully saved the new settings.", MessageBox.MessageButtons.Ok);
        }
    }
}
