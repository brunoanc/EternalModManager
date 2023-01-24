# EternalModManager

A cross platform mod manager for DOOM Eternal, making it easier to set-up and install mods in both Windows and Linux.

## Installing on Linux
### Flatpak (Recommended)

<a href='https://flathub.org/apps/details/com.powerball253.eternalmodmanager'>
    <img width='400' alt='Download on Flathub' src='https://flathub.org/assets/badges/flathub-badge-en.png'/>
</a>

### Snap

<a href='https://snapcraft.io/eternalmodmanager'>
    <img width='400' alt='Get it from the Snap Store' src='https://snapcraft.io/static/images/badges/en/snap-store-black.svg' />
</a>

### AppImage

<a href='https://appimage.github.io/EternalModManager'>
    <img width='400' alt='Download as an AppImage' src='https://docs.appimage.org/_images/download-appimage-banner.svg' />
</a>

### AUR (Arch Linux)
The app is currently available in the [AUR](https://aur.archlinux.org/packages/eternalmodmanager/). You can use your favorite AUR helper to install it, or download and build manually as described in the [Arch wiki](https://aur.archlinux.org/packages/eternalmodmanager/).

## Installing on Windows
Make sure you have the [.NET 7 Runtime](https://dotnet.microsoft.com/en-us/download) installed, then download and extract the latest .zip from the latest release to your DOOM Eternal directory.

## Compiling
First, make sure you have the latest version of the .NET 7 SDK installed. Then clone the repo, and run the following commands:

```
dotnet build -c Release
```

The compiled application will be located in the `EternalModManager/bin/Release/net7.0` folder.

Optionally, you can run the app with the following command:

```
dotnet run -c Release
```
