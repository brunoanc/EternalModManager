using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using EternalModManager.ViewModels;

namespace EternalModManager.Classes;

// Mod info class
public class ModInfo: INotifyPropertyChanged

{
    // INotifyPropertyChanged stuff
    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Mod properties
    private string _name = "";
    private string _fileName = "";
    private bool _isValid;
    private bool _isEnabled;
    private bool _isOnlineSafe;
    private string _onlineSafetyMessage = "";
    private char _onlineSafetyIcon = '\0';
    private IBrush _onlineSafetyColor = Brushes.White;
    private string _author = "";
    private string _description = "";
    private string _version = "";
    private string _loadPriority = "";
    private string _requiredVersion = "";

    // Mod properties getters/setters with NotifyPropertyChanged
    public string Name
    {
        get => _name;
        set
        {
            if (value != _name)
            {
                _name = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            if (value != _fileName)
            {
                _fileName = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool IsValid
    {
        get => _isValid;
        set
        {
            if (value != _isValid)
            {
                _isValid = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value != _isEnabled)
            {
                _isEnabled = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool IsOnlineSafe
    {
        get => _isOnlineSafe;
        set
        {
            if (value != _isOnlineSafe)
            {
                _isOnlineSafe = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string OnlineSafetyMessage
    {
        get => _onlineSafetyMessage;
        set
        {
            if (value != _onlineSafetyMessage)
            {
                _onlineSafetyMessage = value;
                NotifyPropertyChanged();
            }
        }
    }

    public char OnlineSafetyIcon
    {
        get => _onlineSafetyIcon;
        set
        {
            if (value != _onlineSafetyIcon)
            {
                _onlineSafetyIcon = value;
                NotifyPropertyChanged();
            }
        }
    }

    public IBrush OnlineSafetyColor
    {
        get => _onlineSafetyColor;
        set
        {
            if (!_onlineSafetyColor.Equals(value))
            {
                _onlineSafetyColor = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Author
    {
        get => _author;
        set
        {
            if (value != _author)
            {
                _author = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (value != _description)
            {
                _description = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Version
    {
        get => _version;
        set
        {
            if (value != _version)
            {
                _version = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string LoadPriority
    {
        get => _loadPriority;
        set
        {
            if (value != _loadPriority)
            {
                _loadPriority = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string RequiredVersion
    {
        get => _requiredVersion;
        set
        {
            if (value != _requiredVersion)
            {
                _requiredVersion = value;
                NotifyPropertyChanged();
            }
        }
    }

    // Constructor
    public ModInfo(string? name, string fileName, bool isValid, bool isEnabled, bool isOnlineSafe,
        string? author = null, string? description = null, string? version = null, string? loadPriority = null,
        string? requiredVersion = null)
    {
        Name = name ?? " ";
        FileName = fileName;
        IsValid = isValid;
        IsEnabled = isEnabled;
        IsOnlineSafe = isOnlineSafe;
        OnlineSafetyMessage = " ";
        Author = author ?? "Unknown.";
        Description = description ?? "Not specified.";
        Version = version ?? "Not specified.";
        LoadPriority = loadPriority ?? "0";
        RequiredVersion = requiredVersion ?? "Unknown.";
    }
}
