# EternalModManager

A cross platform mod manager for DOOM Eternal, making it easier to set-up and install mods in both Windows and Linux.

## Installing
### Flatpak (Linux)
The app is currently available in [Flathub](https://flathub.org/apps/details/com.powerball253.eternalmodmanager). To install it, make sure you have `flatpak` installed, then run
```
flatpak install flathub com.powerball253.eternalmodmanager
```
and reboot your system. The app should now be available in your DE's menu, or you can run it in your terminal with the following command:
```
flatpak run com.powerball253.eternalmodmanager
```

### Snap (Linux)
The app is currently available in the [Snap Store](https://snapcraft.io/eternalmodmanager). To install it, make sure you have `snap` installed, then run
```
snap install eternalmodmanager
```
and reboot your system. The app should now be available in your DE's menu, or you can run it in your terminal with either of the following commands:
```
eternalmodmanager
snap run eternalmodmanager
```

### AppImage (Linux)
Download the AppImage file from the latest release to your DOOM Eternal directory and run it from there. Alternatively, you can use [AppImageLauncher](https://github.com/TheAssassin/AppImageLauncher) to integrate it into your system.

### AUR (Arch Linux)
The app is currently available in the [AUR](https://aur.archlinux.org/packages/eternalmodmanager/). You can use your favorite AUR helper to install it, or download and build manually as described in the [Arch wiki](https://aur.archlinux.org/packages/eternalmodmanager/).

### Portable executable (Windows)
Download and extract the latest .zip from the latest release to your DOOM Eternal directory.

## Compiling
First, make sure you have the latest version of the .NET 6 SDK installed. Then clone the repo, and run the following commands:

```
dotnet build -c Release
```

The compiled application will be located in the `EternalModManager/bin/Release/net6.0` folder.

Optionally, you can run the app with the following command:

```
dotnet run -c Release
```
