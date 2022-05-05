using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using EternalModManager.Classes;
using EternalModManager.ViewModels;
using EternalModManager.Views;

namespace EternalModManager
{
    public partial class App : Application
    {
        // Game path
        public static string GamePath = "";

        // Config path
        public static string ConfigPath = "";

        // Path to game settings file
        public static string InjectorSettingsPath = "";

        // Path to mod files
        public static string ModsPath = "";

        // Path to disabled mod files
        public static string DisabledModsPath = "";

        // Theme
        public static FluentThemeMode Theme;

        public override void Initialize()
        {
            // Get config path
            string configDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EternalModManager");
            ConfigPath = Path.Join(configDir, "config.json");

            // Read config file
            ConfigFile? config = null;

            if (File.Exists(ConfigPath))
            {
                try
                {
                    string configJson = File.ReadAllText(ConfigPath);
                    config = JsonSerializer.Deserialize<ConfigFile>(configJson, new JsonSerializerOptions()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                }
                catch { }
            }

            // Get theme from config file
            if (config?.Theme != null && config.Theme.ToLower().Equals("light"))
            {
                Theme = FluentThemeMode.Light;
            }
            else
            {
                Theme = FluentThemeMode.Dark;
            }

            // Get game path from arguments
            if (Environment.GetCommandLineArgs().Length > 1)
            {
                GamePath = Environment.GetCommandLineArgs()[1];
            }

            // If no path was provided through arguments, try getting it from config file
            if (String.IsNullOrEmpty(GamePath) && config?.GamePath != null)
            {
                if (File.Exists(Path.Join(config.GamePath, "DOOMEternalx64vk.exe")))
                {
                    GamePath = config.GamePath;
                }
            }

            // If config doesn't have game path value, try cwd
            if (String.IsNullOrEmpty(GamePath) && File.Exists(Path.Join(Directory.GetCurrentDirectory(), "DOOMEternalx64vk.exe")))
            {
                GamePath = Directory.GetCurrentDirectory();
            }

            // Load xaml
            AvaloniaXamlLoader.Load(this);

            // Set theme
            (Styles[0] as FluentTheme)!.SetValue(FluentTheme.ModeProperty, Theme);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
