using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace FileFlow.Sdk.Localization;

/// <summary>
/// Contrato de puerto para servicios de localización e internacionalización dinámica.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    CultureInfo CurrentCulture { get; set; }
    string CurrentLanguage { get; }
    string this[string key] { get; }
    string GetString(string key, string fallback = "");
    string GetFormattedString(string key, string fallbackTemplate, params object?[] args);
    void SetCulture(string cultureName);
    void RegisterResourceManager(ResourceManager resourceManager);
    event EventHandler<CultureInfo>? LanguageChanged;
}
