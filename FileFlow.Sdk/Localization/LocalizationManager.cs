using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace FileFlow.Sdk.Localization;

public class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private readonly List<ResourceManager> _resourceManagers = [];
    private readonly Lock _lock = new();
    private CultureInfo _currentCulture = new("es-ES");

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value)) return;
            _currentCulture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.CurrentUICulture = value;
            OnPropertyChanged(string.Empty);
            OnPropertyChanged("Item[]");
            OnPropertyChanged("Item");
            OnPropertyChanged(nameof(CurrentLanguage));
            LanguageChanged?.Invoke(this, value);
        }
    }

    public string CurrentLanguage => CurrentCulture.TwoLetterISOLanguageName;

    public event EventHandler<CultureInfo>? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void RegisterResourceManager(ResourceManager resourceManager)
    {
        lock (_lock)
        {
            if (!_resourceManagers.Contains(resourceManager))
            {
                _resourceManagers.Add(resourceManager);
            }
        }
    }

    public string this[string key] => GetString(key);

    public string GetString(string key, string fallback = "")
    {
        List<ResourceManager> managers;
        lock (_lock)
        {
            managers = [.. _resourceManagers];
        }

        foreach (var rm in managers)
        {
            try
            {
                string? val = rm.GetString(key, _currentCulture);
                if (!string.IsNullOrEmpty(val))
                {
                    return val;
                }
            }
            catch
            {
                // Ignorar excepciones de recursos individuales para continuar la búsqueda
            }
        }
        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }

    public void SetCulture(string cultureCode)
    {
        CurrentCulture = new CultureInfo(cultureCode);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
