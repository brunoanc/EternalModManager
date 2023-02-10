using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace EternalModManager.ViewModels;

public class MessageBoxViewModel : ViewModelBase
{
    // Theme colors
    public static Color ThemeColor => App.Theme.Equals(FluentThemeMode.Dark) ? Colors.Black : Colors.White;
    public static IBrush FontColor => App.Theme.Equals(FluentThemeMode.Dark) ? (new BrushConverter().ConvertFrom("#C8C8C8") as IBrush)! : Brushes.Black;
    public static IBrush Gray => (new BrushConverter().ConvertFrom(App.Theme.Equals(FluentThemeMode.Dark) ? "#5D5D5D" : "#E1E1E1") as IBrush)!;
    public static IBrush HoverGray => (new BrushConverter().ConvertFrom(App.Theme.Equals(FluentThemeMode.Dark) ? "#686868" : "#ECECEC") as IBrush)!;
}
