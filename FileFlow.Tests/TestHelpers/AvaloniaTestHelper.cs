using System;
using System.Globalization;
using FileFlow.Sdk.Localization;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Helper de compatibilidad portable para pruebas de internacionalización y ejecución síncrona,
/// sin dependencias del runtime o paquetes de Avalonia.
/// </summary>
public static class AvaloniaTestHelper
{
    public const string PinnedLanguage = "es-ES";

    public static void EnsureInitialized() { }

    public static void SetCultureOnUI(string cultureName)
    {
        LocalizationManager.Instance.SetCulture(cultureName);
    }

    public static void RunOnUI(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public static T RunOnUI<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return func();
    }
}
