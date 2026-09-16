using System.Collections.Generic;
using System.Collections.ObjectModel;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

/// <summary>Sección del editor del Theme Studio (agrupa filas por familia de tokens).</summary>
public sealed class ThemeSettingSectionViewModel
{
    public ThemeSettingSectionViewModel(string titleKey, IEnumerable<ThemeSettingRowViewModel> rows)
    {
        TitleKey = titleKey;
        Rows = new ObservableCollection<ThemeSettingRowViewModel>(rows);
    }

    public string TitleKey { get; }

    public string Title => LocalizationManager.Instance.GetString(TitleKey, TitleKey);

    public ObservableCollection<ThemeSettingRowViewModel> Rows { get; }
}
