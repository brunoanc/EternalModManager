using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using EternalModManager.ViewModels;

namespace EternalModManager.Views
{
    public partial class MessageBox : Window
    {
        // Message box result
        private static MessageResult _result;

        // Message type enum
        public enum MessageType
        {
            Information,
            Warning,
            Error
        }

        // Message button combinations enum
        public enum MessageButtons
        {
            Ok,
            YesCancel,
            None
        }

        // Message box result enum
        public enum MessageResult
        {
            Ok,
            Yes,
            Cancel
        }

        // Constructor
        public MessageBox()
        {
            // Init window components
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            // Set height
            Height = _height;

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

                // Make custom close button visible
                this.FindControl<Button>("CloseButton")!.IsVisible = true;
            }
            else
            {
                // Remove custom close button for Windows
                var titleBar = this.FindControl<Canvas>("MessageTitleBar")!;
                titleBar.Children.Remove(this.FindControl<Button>("CloseButton")!);

                // Linux specific changes
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Remove custom title
                    titleBar.Children.Remove(this.FindControl<TextBlock>("MessageTitle")!);

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

        // Make OK button close the window
        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            // Get pressed button and set result
            switch (((Button)sender!).Content)
            {
                case "OK":
                    _result = MessageResult.Ok;
                    break;
                case "Yes":
                    _result = MessageResult.Yes;
                    break;
                case "Cancel":
                    _result = MessageResult.Cancel;
                    break;
            }

            Close();
        }

        // Close window on close button click
        private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        // Window height
        private static int _height = 140;

        // Create and display message box
        public static async Task<MessageResult> Show(Window parent, MessageType type, string text, MessageButtons buttons, int height = 140)
        {
            // Set height
            _height = height;

            // Set text
            var msgbox = new MessageBox
            {
                DataContext = new MessageBoxViewModel()
            };

            msgbox.FindControl<TextBlock>("Text")!.Text = text;

            // Set title and image
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>()!;

            switch (type)
            {
                case MessageType.Information:
                    msgbox.FindControl<TextBlock>("MessageTitle")!.Text = "Information";
                    msgbox.FindControl<Image>("MessageIcon")!.Source = new Bitmap(assets.Open(new Uri("avares://EternalModManager/Assets/info.png")));
                    break;
                case MessageType.Warning:
                    msgbox.FindControl<TextBlock>("MessageTitle")!.Text = "Warning";
                    msgbox.FindControl<Image>("MessageIcon")!.Source = new Bitmap(assets.Open(new Uri("avares://EternalModManager/Assets/error.png")));
                    break;
                case MessageType.Error:
                    msgbox.FindControl<TextBlock>("MessageTitle")!.Text = "Error";
                    msgbox.FindControl<Image>("MessageIcon")!.Source = new Bitmap(assets.Open(new Uri("avares://EternalModManager/Assets/error.png")));
                    break;
            }

            // Set buttons and default result
            var okButton = msgbox.FindControl<Button>("OkButton")!;
            var cancelButton = msgbox.FindControl<Button>("CancelButton")!;

            switch (buttons)
            {
                case MessageButtons.Ok:
                    okButton.IsVisible = true;
                    _result = MessageResult.Ok;
                    break;
                case MessageButtons.YesCancel:
                    okButton.IsVisible = true;
                    cancelButton.IsVisible = true;
                    okButton.Content = "Yes";
                    _result = MessageResult.Cancel;
                    break;
                case MessageButtons.None:
                    _result = MessageResult.Ok;
                    break;
            }

            // Disable parent
            var parentTopLevelPanel = parent.FindControl<Panel>("TopLevelPanel")!;
           parentTopLevelPanel.IsEnabled = false;
           parentTopLevelPanel.Opacity = 0.7;

           // Show window
            await msgbox.ShowDialog(parent);

            // Re-enable parent
            parentTopLevelPanel.Opacity = 1;
            parentTopLevelPanel.IsEnabled = true;

            return _result;
        }
    }
}
