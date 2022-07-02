using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using EternalModManager.Views;
using EternalModManager.Classes;

namespace EternalModManager.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        // App title
        public static string AppTitle => $"EternalModManager v{Assembly.GetEntryAssembly()!.GetName().Version!.ToString()[..^2]} by PowerBall253";

        // Bindable mod list for DataGrid
        private readonly ReadOnlyObservableCollection<ModInfo> _modsBindableList;
        public ReadOnlyObservableCollection<ModInfo> ModsBindableList => _modsBindableList;

        // Source mod list, used by DynamicData
        public readonly SourceList<ModInfo> ModsList = new();

        // Currently displayed mod
        private ModInfo _currentModInfo = new("-", "", false, false, false, "-", "-", "-", "-", "-");

        public ModInfo CurrentModInfo
        {
            get => _currentModInfo;
            set => this.RaiseAndSetIfChanged(ref _currentModInfo, value);
        }

        // Theme colors
        public static Color ThemeColor => App.Theme.Equals(FluentThemeMode.Dark) ? Colors.Black : Colors.White;
        public static IBrush FontColor => App.Theme.Equals(FluentThemeMode.Dark) ? (new BrushConverter().ConvertFrom("#C8C8C8") as IBrush)! : Brushes.Black;
        public static IBrush Gray => (new BrushConverter().ConvertFrom(App.Theme.Equals(FluentThemeMode.Dark) ? "#5D5D5D" : "#E1E1E1") as IBrush)!;
        public static IBrush HoverGray => (new BrushConverter().ConvertFrom(App.Theme.Equals(FluentThemeMode.Dark) ? "#686868" : "#ECECEC") as IBrush)!;

        // Keywords for online safety check
        private readonly string[] _onlineSafeModNameKeywords =
        {
            "/eternalmod/", ".tga", ".png", ".swf", ".bimage", "/advancedscreenviewshake/", "/audiolog/", "/audiologstory/", "/automap/", "/automapplayerprofile/",
            "/automapproperties/", "/automapsoundprofile/", "/env/", "/font/", "/fontfx/", "/fx/", "/gameitem/", "/globalfonttable/", "/gorebehavior/",
            "/gorecontainer/", "/gorewounds/", "/handsbobcycle/", "/highlightlos/", "/highlights/", "/hitconfirmationsoundsinfo/", "/hud/", "/hudelement/",
            "/lightrig/", "/lodgroup/", "/material2/", "/md6def/", "/modelasset/", "/particle/", "/particlestage/", "/renderlayerdefinition/", "/renderparm/",
            "/renderparmmeta/", "/renderprogflag/", "/ribbon2/", "/rumble/", "/soundevent/", "/soundpack/", "/soundrtpc/", "/soundstate/", "/soundswitch/",
            "/speaker/", "/staticimage/", "/swfresources/", "/uianchor/", "/uicolor/", "/weaponreticle/", "/weaponreticleswfinfo/", "/entitydef/light/", "/entitydef/fx",
            "/entitydef/", "/impacteffect/", "/uiweapon/", "/globalinitialwarehouse/", "/globalshell/", "/warehouseitem/", "/warehouseofflinecontainer/", "/tooltip/",
            "/livetile/", "/tutorialevent/", "/maps/game/dlc/", "/maps/game/dlc2/", "/maps/game/hub/", "/maps/game/shell/", "/maps/game/sp/", "/maps/game/tutorials/",
            "/decls/campaign"
        };

        private readonly string[] _unsafeResourceNameKeywords =
        {
            "gameresources", "pvp", "shell", "warehouse"
        };

        // Check if mod is online safe
        private bool IsModOnlineSafe(string modPath)
        {
            var assetsInfoJsons = new List<ZipArchiveEntry>();

            // Iterate through zip's entries
            using var modZip = ZipFile.OpenRead(modPath);

            foreach (var modFile in modZip.Entries)
            {
                string modFileEntry = modFile.FullName.ToLower();

                // Skip directories
                if (modFileEntry.EndsWith('/'))
                {
                    continue;
                }

                // Skip top-level files
                if (!modFileEntry.Contains('/'))
                {
                    continue;
                }

                // Allow hidden system files that may end up included accidentally
                if (modFileEntry.EndsWith("desktop.ini") || modFileEntry.EndsWith(".ds_store"))
                {
                    continue;
                }

                string containerName = modFileEntry.Split('/')[0];
                string modName = modFileEntry[(containerName.Length + 1)..];
                string soundContainerPath = Path.Join(App.GamePath, $"base/sound/soundbanks/pc/{containerName}.snd");

                // Allow sound files
                if (File.Exists(soundContainerPath))
                {
                    continue;
                }

                // Allow streamdb mods
                if (containerName.Equals("streamdb"))
                {
                    continue;
                }

                // Save AssetsInfo JSON files to be handled later
                if (modFileEntry.StartsWith("EternalMod/assetsinfo") && modFileEntry.EndsWith(".json"))
                {
                    assetsInfoJsons.Add(modFile);
                }

                // Check if mod is modifying an online-unsafe resource
                bool isModifyingUnsafeResouce = _unsafeResourceNameKeywords.Any(keyword => containerName.StartsWith(keyword));

                // Files with .lwo extension are unsafe
                if (Path.GetExtension(modFileEntry).Contains(".lwo") && isModifyingUnsafeResouce)
                {
                    return false;
                }

                // Allow modification of anything outside 'generated/decls/'
                if (!modName.StartsWith("generated/decls"))
                {
                    continue;
                }

                // Do not allow mods to modify non-whitelisted files in unsafe resources
                if (!_onlineSafeModNameKeywords.Any(keyword => modName.Contains(keyword)) && isModifyingUnsafeResouce)
                {
                    return false;
                }
            }

            // Don't allow injecting files into the online-unsafe resources
            foreach (var assetsInfoEntry in assetsInfoJsons)
            {
                string resourceName = assetsInfoEntry.FullName.Split('/')[0];
                string assetsInfoJson;

                using (var reader = new StreamReader(assetsInfoEntry.Open()))
                {
                    assetsInfoJson = reader.ReadToEnd();
                }

                var assetsInfo = JsonSerializer.Deserialize<AssetsInfo>(assetsInfoJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (assetsInfo?.Resources?.Count > 0 && _unsafeResourceNameKeywords.Any(keyword => resourceName.StartsWith(keyword)))
                {
                    return false;
                }
            }

            return true;
        }

        // Load the given mod into the list
        private void LoadModIntoList(string modFile, bool isEnabled, bool onlyLoadOnlineSafe, List<ModInfo> bufferModList)
        {
            ModInfo modInfo;

            // Read mod info from EternalMod.json
            try
            {
                using var modZip = ZipFile.OpenRead(modFile);
                var eternalModJsonEntry = modZip.GetEntry("EternalMod.json");

                if (eternalModJsonEntry != null)
                {
                    string eternalModJson;

                    using (var reader = new StreamReader(eternalModJsonEntry.Open()))
                    {
                        eternalModJson = reader.ReadToEnd();
                    }

                    var eternalMod = JsonSerializer.Deserialize<EternalMod>(eternalModJson, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    })!;

                    modInfo = new ModInfo(eternalMod.Name, Path.GetFileName(modFile), true, isEnabled,
                        IsModOnlineSafe(modFile), eternalMod.Author, eternalMod.Description, eternalMod.Version,
                        eternalMod.LoadPriority?.ToString(), eternalMod.RequiredVersion?.ToString());
                }
                else
                {
                    modInfo = new ModInfo(Path.GetFileName(modFile), Path.GetFileName(modFile), true, isEnabled, IsModOnlineSafe(modFile));
                }
            }
            catch
            {
                modInfo = new ModInfo(Path.GetFileName(modFile), Path.GetFileName(modFile), false, isEnabled, false);
            }

            // Set online safety message
            if (!modInfo.IsValid)
            {
                modInfo.OnlineSafetyIcon = '✗';
                modInfo.OnlineSafetyMessage = "Invalid .zip file.";
                modInfo.OnlineSafetyColor = App.Theme.Equals(FluentThemeMode.Dark) ? Brushes.OrangeRed : Brushes.Red;
            }
            else if (modInfo.IsOnlineSafe)
            {
                modInfo.OnlineSafetyIcon = '✓';
                modInfo.OnlineSafetyMessage = "This mod is safe for use in public matches.";
                modInfo.OnlineSafetyColor = App.Theme.Equals(FluentThemeMode.Dark) ? Brushes.YellowGreen : Brushes.Green;
            }
            else if (onlyLoadOnlineSafe)
            {
                modInfo.OnlineSafetyIcon = '！';
                modInfo.OnlineSafetyMessage = "This mod is not safe for use in public matches. It will not be loaded.";
                modInfo.OnlineSafetyColor = App.Theme.Equals(FluentThemeMode.Dark) ? Brushes.Orange : Brushes.DarkOrange;
            }
            else if (isEnabled)
            {
                modInfo.OnlineSafetyIcon = '！';
                modInfo.OnlineSafetyMessage = "This mod is not safe for use in public matches. Public Battlemode matches will be disabled.";
                modInfo.OnlineSafetyColor = App.Theme.Equals(FluentThemeMode.Dark) ? Brushes.OrangeRed : Brushes.Red;
            }
            else
            {
                modInfo.OnlineSafetyIcon = '！';
                modInfo.OnlineSafetyMessage = "This mod is not safe for use in public matches.";
                modInfo.OnlineSafetyColor = App.Theme.Equals(FluentThemeMode.Dark) ? Brushes.OrangeRed : Brushes.Red;
            }

            // Add mod to list
            bufferModList.Add(modInfo);
        }

        // Get all mods and add them to the mod list
        private void GetMods()
        {
            // Buffer to store newly loaded mods
            var bufferModList = new List<ModInfo>();

            // Check if only online safe mods should be loaded
            bool onlyLoadOnlineSafe = false;

            try {
                if (File.Exists(App.InjectorSettingsPath) && File.ReadAllText(App.InjectorSettingsPath).Contains(":ONLINE_SAFE=1"))
                {
                    onlyLoadOnlineSafe = true;
                }
            }
            catch { }

            // Get enabled mods
            foreach (var modFile in Directory.EnumerateFiles(App.ModsPath, "*.zip", SearchOption.TopDirectoryOnly))
            {
                LoadModIntoList(modFile, true, onlyLoadOnlineSafe, bufferModList);
            }

            // Get disabled mods
            foreach (var modFile in Directory.EnumerateFiles(App.DisabledModsPath, "*.zip", SearchOption.TopDirectoryOnly))
            {
                LoadModIntoList(modFile, false, onlyLoadOnlineSafe, bufferModList);
            }

            // Sort buffer mod list
            bufferModList.Sort(SortExpressionComparer<ModInfo>.Ascending(modInfo => modInfo.FileName));

            // Update mods list non-destructively
            ModsList.Edit(innerList =>
            {
                if (bufferModList.Count < innerList.Count)
                {
                    innerList.RemoveRange(bufferModList.Count, innerList.Count - bufferModList.Count);
                }

                for (int i = 0; i < innerList.Count; i++)
                {
                    innerList[i].Name = bufferModList[i].Name;
                    innerList[i].FileName = bufferModList[i].FileName;
                    innerList[i].IsValid = bufferModList[i].IsValid;
                    innerList[i].IsEnabled = bufferModList[i].IsEnabled;
                    innerList[i].IsOnlineSafe = bufferModList[i].IsOnlineSafe;
                    innerList[i].OnlineSafetyMessage = bufferModList[i].OnlineSafetyMessage;
                    innerList[i].OnlineSafetyIcon = bufferModList[i].OnlineSafetyIcon;
                    innerList[i].OnlineSafetyColor = bufferModList[i].OnlineSafetyColor;
                    innerList[i].Author = bufferModList[i].Author;
                    innerList[i].Description = bufferModList[i].Description;
                    innerList[i].Version = bufferModList[i].Version;
                    innerList[i].LoadPriority = bufferModList[i].LoadPriority;
                    innerList[i].RequiredVersion = bufferModList[i].RequiredVersion;
                }

                if (bufferModList.Count > innerList.Count)
                {
                    innerList.AddRange(bufferModList.Skip(innerList.Count));
                }
            });
        }

        // Create mod directories
        private static void CreateModDirectories()
        {
            try
            {
                if (!File.Exists(App.ModsPath))
                {
                    Directory.CreateDirectory(App.ModsPath);
                }

                if (!File.Exists(App.DisabledModsPath))
                {
                    Directory.CreateDirectory(App.DisabledModsPath);
                }
            }
            catch { }
        }

        // Reload mods
        public void ReloadMods()
        {
            CreateModDirectories();
            GetMods();
        }

        // Watchers
        public FileSystemWatcher? ModsFolderWatcher;
        public FileSystemWatcher? DisabledModsFolderWatcher;
        public FileSystemWatcher? SettingsFileWatcher;

        // Determines whether mods should be reloaded on filesystem changes
        //public bool ShouldWatcherReloadMods = true;

        // Init the filesystem watcher
        public void InitWatcher()
        {
            // Create directories to watch
            CreateModDirectories();

            // Init watchers
            ModsFolderWatcher = new FileSystemWatcher(App.ModsPath)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                Filter = "*.zip"
            };

            DisabledModsFolderWatcher = new FileSystemWatcher(App.DisabledModsPath)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                Filter = "*.zip"
            };

            SettingsFileWatcher = new FileSystemWatcher(App.GamePath)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                Filter = "EternalModInjector Settings.txt"
            };

            // Reload mods on filesystem changes
            ModsFolderWatcher.Changed += OnChanged;
            ModsFolderWatcher.Created += OnChanged;
            ModsFolderWatcher.Deleted += OnChanged;
            ModsFolderWatcher.Renamed += OnChanged;

            DisabledModsFolderWatcher.Changed += OnChanged;
            DisabledModsFolderWatcher.Created += OnChanged;
            DisabledModsFolderWatcher.Deleted += OnChanged;
            DisabledModsFolderWatcher.Renamed += OnChanged;

            SettingsFileWatcher.Changed += OnChanged;
            SettingsFileWatcher.Created += OnChanged;
            SettingsFileWatcher.Deleted += OnChanged;
            SettingsFileWatcher.Renamed += OnChanged;

            // Load mods
            ReloadMods();
        }

        // Filesystem watcher callback
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            // Load mods
            lock (ModsList)
            {
                ReloadMods();
            }
        }

        // Commands for binding
        public ReactiveCommand<Unit, Unit> InstallZipsCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenLocationCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

        // Constructor
        public MainWindowViewModel()
        {
            // Init commands
            InstallZipsCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                // Open file dialog
                var fileDialog = new OpenFileDialog
                {
                    Title = "Open .zip mod files to install",
                    AllowMultiple = true,
                    Filters = new List<FileDialogFilter>
                    {
                        new()
                        {
                            Name = "Zip files",
                            Extensions = new List<string>
                            {
                                "zip"
                            }
                        }
                    }
                };

                // Get selected files
                string[]? results = await fileDialog.ShowAsync((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!);

                if (results == null || results.Length == 0)
                {
                    return;
                }

                // Copy mods to mods folder
                foreach (var zipFile in results)
                {
                    try
                    {
                        File.Copy(zipFile, Path.Join(App.ModsPath, Path.GetFileName(zipFile)), false);
                    }
                    catch { }
                }
            });

            OpenLocationCommand = ReactiveCommand.Create(() =>
            {
                // Get selected item
                var modsGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("ModsList")!;
                var selectedItem = modsGrid.SelectedItem as ModInfo;

                if (selectedItem == null)
                {
                    return;
                }

                // Open directory
                Process.Start(new ProcessStartInfo
                {
                    FileName = selectedItem.IsEnabled ? App.ModsPath : App.DisabledModsPath,
                    UseShellExecute = true
                });
            });

            ToggleCommand = ReactiveCommand.Create(() =>
            {
                // Get selected item
                var modsGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("ModsList")!;
                var selectedItem = modsGrid.SelectedItem as ModInfo;

                if (selectedItem == null)
                {
                    return;
                }

                // Get src and target directories for moving
                var srcDir = selectedItem.IsEnabled ? App.ModsPath : App.DisabledModsPath;
                var destDir = selectedItem.IsEnabled ? App.DisabledModsPath : App.ModsPath;

                // Move mod to target dir
                try
                {
                    File.Move(Path.Join(srcDir, selectedItem.FileName), Path.Join(destDir, selectedItem.FileName), false);
                }
                catch { }
            });

            DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                // Get selected item
                var modsGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("ModsList")!;
                var selectedItem = modsGrid.SelectedItem as ModInfo;

                if (selectedItem == null)
                {
                    return;
                }

                // Ask for confirmation
                var result = await MessageBox.Show((Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!,
                    MessageBox.MessageType.Warning, $"Are you sure you want to delete the selected mod?\n\n{selectedItem.FileName}", MessageBox.MessageButtons.YesCancel);

                if (result != MessageBox.MessageResult.Yes)
                {
                    return;
                }

                // Get mod's path
                var fileDir = selectedItem.IsEnabled ? App.ModsPath : App.DisabledModsPath;
                var fileName = Path.Join(fileDir, selectedItem.FileName);

                // Delete mod
                try
                {
                    File.Delete(fileName);
                }
                catch { }
            });

            // Init DataGrid's mod list
            ModsList.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _modsBindableList)
                .DisposeMany()
                .Subscribe();
        }
    }
}
