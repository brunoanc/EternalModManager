using System;
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
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
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

        // Custom window drag implementation for Windows
        // Taken from https://github.com/FrankenApps/Avalonia-CustomTitleBarTemplate
        private bool _isPointerPressed = false;
        private PixelPoint _startPosition = new(0, 0);
        private Point _mouseOffsetToOrigin = new(0, 0);

        private void HandlePotentialDrop(object? sender, PointerReleasedEventArgs e)
        {
            var pos = e.GetPosition(this.FindControl<Panel>("MessageTitleBar"));
            _startPosition = new PixelPoint((int)(_startPosition.X + pos.X - _mouseOffsetToOrigin.X), (int)(_startPosition.Y + pos.Y - _mouseOffsetToOrigin.Y));
            Position = _startPosition;
            _isPointerPressed = false;
        }

        private void HandlePotentialDrag(object? sender, PointerEventArgs e)
        {
            if (_isPointerPressed)
            {
                var pos = e.GetPosition(this.FindControl<Panel>("MessageTitleBar"));
                _startPosition = new PixelPoint((int)(_startPosition.X + pos.X - _mouseOffsetToOrigin.X), (int)(_startPosition.Y + pos.Y - _mouseOffsetToOrigin.Y));
                Position = _startPosition;
            }
        }

        private void BeginListenForDrag(object? sender, PointerPressedEventArgs e)
        {
            _startPosition = Position;
            _mouseOffsetToOrigin = e.GetPosition(this.FindControl<Panel>("MessageTitleBar"));
            _isPointerPressed = true;
        }

        // Create and display message box
        public static async Task<MessageResult> Show(Window parent, MessageType type, string text, MessageButtons buttons)
        {
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

        // Initialize window
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Remove acrylic blur on light theme
            if (App.Theme.Equals(FluentThemeMode.Light))
            {
                Background = Brushes.White;
                TransparencyLevelHint = WindowTransparencyLevel.None;
                this.FindControl<Panel>("TopLevelPanel")!.Children.Remove(this.FindControl<ExperimentalAcrylicBorder>("AcrylicBorder")!);
            }

            // Make window not maximizable
            Opened += (_, _) =>
            {
                MinWidth = Width;
                MinHeight = Height;
                MaxWidth = Width;
                MaxHeight = Height;
            };

            // OS-specific changes
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows requires a custom titlebar due to system chrome issues
                // Remove default titlebar buttons
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

                // Make custom close button visible
                this.FindControl<Button>("CloseButton")!.IsVisible = true;

                // Set drag-and-drop for custom title bar
                var titleBar = this.FindControl<Panel>("MessageTitleBar")!;
                titleBar.IsHitTestVisible = true;
                titleBar.PointerPressed += BeginListenForDrag;
                titleBar.PointerMoved += HandlePotentialDrag;
                titleBar.PointerReleased += HandlePotentialDrop;
            }
            else
            {
                // Remove custom close button for Windows
                this.FindControl<Panel>("MessageTitleBar")!.Children.Remove(this.FindControl<Button>("CloseButton")!);

                // Linux specific changes
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Remove custom title
                    this.FindControl<Panel>("MessageTitleBar")!.Children.Remove(this.FindControl<TextBlock>("MessageTitle")!);

                    // Disable acrylic blur
                    TransparencyLevelHint = WindowTransparencyLevel.None;
                }
            }
        }
    }
}