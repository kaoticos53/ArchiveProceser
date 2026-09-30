using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;
/// <summary>
/// La guardia de la <b>PORTABILIDAD</b> de los diálogos de nodo del host Uno (hito 258): el editor de texto y el
/// catálogo de variables son VISTAS de los view models portables —los mismos de la versión anterior— y sus textos son
/// los de la versión anterior, copiados clave por clave en los dos idiomas.
///
/// <para><b>Qué protege</b>: que las dos vistas monten el view model portable y no una copia de su lógica
/// (nada de construir el view model ni de guardar el resultado por su cuenta); que el valor viaje por sus
/// métodos; y que cada rótulo del host tenga su clave de la versión anterior con el mismo texto —una traducción propia
/// del host sería otra interfaz—.</para>
///
/// <para><b>Por qué comparten archivo</b>: los dos sujetos son la MISMA pregunta —¿esto es el producto o una
/// copia?— mirada en la lógica y en el texto. El catálogo y las filas viven en
/// <c>UnoNodeDialogCatalogGuardTests</c>.</para>
/// </summary>
public class UnoDialogPortabilityGuardTests
{
    private const string WindowService = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string EditorBodyXaml = "FileFlow.App.Uno/Controls/TextEditorDialogBody.xaml";
    private const string EditorBodyCode = "FileFlow.App.Uno/Controls/TextEditorDialogBody.xaml.cs";
    private const string PickerBodyXaml = "FileFlow.App.Uno/Controls/VariablePickerDialogBody.xaml";
    private const string PickerBodyCode = "FileFlow.App.Uno/Controls/VariablePickerDialogBody.xaml.cs";
    private const string InspectorCode = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";
    private const string PresetBodyXaml = "FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml";
    private const string PresetBodyCode = "FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml.cs";
    private const string KeysBodyXaml = "FileFlow.App.Uno/Controls/PasswordManagerBody.xaml";
    private const string KeysBodyCode = "FileFlow.App.Uno/Controls/PasswordManagerBody.xaml.cs";
    private const string CardViewXaml = "FileFlow.App.Uno/Controls/NodeCardView.xaml";

    /// <summary>El LIENZO: donde vive el menú del cable (la conexión que el usuario quiere borrar).</summary>
    private const string CanvasCode = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";

    /// <summary>La MITAD DE LOS CABLES del lienzo: el sitio del rótulo del menú del cable.</summary>
    private const string CanvasWiresCode = "FileFlow.App.Uno/Controls/EditorCanvasControl.Wires.cs";
    private const string CardViewModelCode = "FileFlow.App.Uno/Controls/NodeCardViewModel.cs";
    private const string StringsEnglish = "FileFlow.App.Uno/Resources/Strings.resx";
    private const string StringsSpanish = "FileFlow.App.Uno/Resources/Strings.es.resx";

    /// <summary>Las cadenas del núcleo portable de una fila de parámetro: la referencia de paridad de esta superficie.</summary>
    private const string CoreStringsEnglish = "FileFlow.App.Core/Resources/Strings.resx";
    private const string CoreStringsSpanish = "FileFlow.App.Core/Resources/Strings.es.resx";

    /// <summary>
    /// Las familias que NO son del host: cada una la declara el diccionario de SU plugin y sólo él. Ese
    /// diccionario lo registra el cargador al cargar el ensamblado (<c>PluginLoader.RegisterPluginResources</c>),
    /// y no se copia en el del host: la copia sería la tercera, y por orden de registro TAPARÍA a la del plugin,
    /// así que un retoque de una sola de las dos se vería como una traducción distinta de la misma superficie.
    /// </summary>
    private static readonly IReadOnlyList<(string Family, string Plugin)> PluginFamilies =
    [
        ("PresetManager_", "FileFlow.Plugin.Integrations"),
        ("PasswordManager_", "FileFlow.Plugin.Archives"),
    ];

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));
    // ─────────────────────────────────────────────────────────────────────────────
    // 1. Los diálogos son VISTAS de los view models portables
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheDialogs_ShouldBeViewsOfThePortableViewModels_NotCopiesOfThem()
    {
        string service = Code(WindowService);

        // Los dos diálogos construyen los view models PORTABLES: los mismos que envuelve la versión anterior.
        service.Should().Contain("new TextEditorDialogViewModel(");
        service.Should().Contain("new VariablePickerViewModel(");

        // Y el valor no se recalcula aquí: sale de los métodos del view model, los mismos que lee la
        // versión anterior al cerrar su ventana.
        service.Should().Contain("vm.SaveResult();");
        service.Should().Contain("string token = vm.SelectedToken;");

        // El payload es el que el núcleo manda: el parámetro (texto) y la petición del catálogo.
        service.Should().Contain("payload is NodeParameterViewModel parameter");
        service.Should().Contain("payload as VariablePickerRequest");

        // Las vistas se atan a las propiedades del view model, no a un estado propio.
        Read(EditorBodyXaml).Should().Contain("Text=\"{Binding Text, Mode=TwoWay}\"");
        Read(EditorBodyXaml).Should().Contain("ItemsSource=\"{Binding FilteredSideVariables}\"");
        Read(EditorBodyXaml).Should().Contain("Visibility=\"{Binding IsSidePanelVisible,");
        Read(PickerBodyXaml).Should().Contain("ItemsSource=\"{Binding FilteredVariables}\"");
        Read(PickerBodyXaml).Should().Contain("SelectedItem=\"{Binding SelectedVariable, Mode=TwoWay}\"");
        Read(PickerBodyXaml).Should().Contain("Text=\"{Binding SearchText, Mode=TwoWay}\"");

        // La inserción en el editor es la del view model (inserta en el punto del cursor y devuelve dónde
        // queda), no un pegado propio.
        Code(EditorBodyCode).Should().Contain("vm.InsertTokenAt(EditorBox.SelectionStart, item.Token)");

        // Y ninguna de las dos vistas guarda el resultado por su cuenta.
        foreach (string body in new[] { Code(EditorBodyCode), Code(PickerBodyCode) })
        {
            body.Should().NotContain("new TextEditorDialogViewModel(",
                "el view model viene del catálogo de diálogos; construirlo en la vista crearía un segundo estado");
            body.Should().NotContain("new VariablePickerViewModel(");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. Los textos son los de la versión anterior, en los dos idiomas
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>De la clave del host a la clave del diccionario de la versión anterior: el texto se COPIA, no se re-traduce.</summary>
    private static IReadOnlyList<(string Host, string Desktop)> SharedTexts() =>
    [
        ("Uno_Dialog_TextEditor_Title", "TextEditor_WindowTitle"),
        ("Uno_Dialog_TextEditor_InsertVar", "TextEditor_InsertVar"),
        ("Uno_Dialog_TextEditor_Clear", "TextEditor_Clear"),
        ("Uno_Dialog_TextEditor_Preview", "TextEditor_EvaluatedPreview"),
        ("Uno_Dialog_TextEditor_VariablesHeader", "Node_VariablesHeader"),
        ("Uno_Dialog_TextEditor_Save", "TextEditor_SaveBtn"),
        ("Uno_Dialog_Cancel", "TextEditor_CancelBtn"),
        ("Uno_Dialog_OpenEditorToolTip", "TextEditor_Header"),
        ("Uno_Dialog_InsertVariableToolTip", "Node_Param_InsertVariableToolTip"),
        ("Uno_Dialog_VarPicker_Title", "VarPicker_WindowTitle"),
        ("Uno_Dialog_VarPicker_SearchPlaceholder", "VarPicker_SearchPlaceholder"),
        ("Uno_Dialog_VarPicker_Insert", "VarPicker_InsertBtn"),
        ("Uno_Dialog_VarPicker_NoResults", "VarPicker_NoResults"),
        ("Uno_Dialog_VarPicker_DetailTitle", "VarPicker_DetailTitle"),
        ("Uno_Dialog_VarPicker_TokenLabel", "VarPicker_TokenLabel"),
        ("Uno_Dialog_VarPicker_DescriptionLabel", "VarPicker_DescriptionLabel"),
        ("Uno_Dialog_VarPicker_CategoryLabel", "VarPicker_CategoryLabel"),
        ("Uno_Dialog_VarPicker_SourceLabel", "VarPicker_SourceLabel"),
        ("Uno_Dialog_VarPicker_SampleLabel", "VarPicker_SamplePreviewLabel"),
        ("Uno_Dialog_VarPicker_CopyToken", "VarPicker_CopyToken"),

        // El menú del CABLE del lienzo: el rótulo con el que la versión anterior borra una conexión es el suyo.
        ("Uno_Connection_Delete", "DeleteConnection"),
    ];

    [Fact]
    public void TheSharedTexts_ShouldMatchTheCoreOnes_InBothLanguages()
    {
        foreach (var (hostPath, desktopPath) in new[]
                 {
                     (StringsEnglish, CoreStringsEnglish),
                     (StringsSpanish, CoreStringsSpanish),
                 })
        {
            var host = HostDictionaries.Of(hostPath);
            var desktop = HostDictionaries.Of(desktopPath);
            var wrong = new List<string>();

            foreach (var (hostKey, desktopKey) in SharedTexts())
            {
                if (!host.TryGetValue(hostKey, out string? hostValue))
                {
                    wrong.Add($"{hostKey}: falta en {hostPath}");
                    continue;
                }

                if (!desktop.TryGetValue(desktopKey, out string? desktopValue))
                {
                    wrong.Add($"{desktopKey}: falta en {desktopPath} (la referencia)");
                    continue;
                }

                if (!string.Equals(hostValue, desktopValue, StringComparison.Ordinal))
                {
                    wrong.Add($"{hostKey}='{hostValue}' contra {desktopKey}='{desktopValue}'");
                }
            }

            wrong.Should().BeEmpty(
                "los textos de los dos diálogos son los de la versión anterior, copiados: una traducción propia sería "
                + $"otra interfaz ({Path.GetFileName(hostPath)})");
        }
    }

    [Fact]
    public void EveryTextUsedByTheDialogs_ShouldExistInBothHostDictionaries()
    {
        var english = HostDictionaries.Of(StringsEnglish);
        var spanish = HostDictionaries.Of(StringsSpanish);
        string views = Code(EditorBodyCode) + Code(PickerBodyCode) + Code(WindowService)
            + Code(EditorBodyXaml) + Code(PickerBodyXaml)
            + Code(PresetBodyCode) + Code(PresetBodyXaml) + Code(InspectorCode)
            + Code(KeysBodyCode) + Code(KeysBodyXaml)
            + Code(CardViewXaml) + Code(CardViewModelCode) + Code(CanvasCode) + Code(CanvasWiresCode);

        // Las claves que las superficies del host citan: las suyas (`Uno_…`), las de la FILA (`Node_Param_…`, que
        // son las de la versión anterior) y las que trae consigo la superficie del plugin (`PresetManager_…` /
        // `PasswordManager_…`), con el rótulo del conmutador de parámetros de la tarjeta (`ToggleParametersToolTip`).
        var cited = Regex.Matches(views, @"""((?:Uno|PresetManager|PasswordManager|Node_Param)_[A-Za-z_]+|ToggleParametersToolTip)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        cited.Should().NotBeEmpty("los diálogos del host citan sus claves por clave, no por literales incrustados");
        cited.Should().Contain("PresetManager_WindowTitle",
            "el gestor de presets tiene que leerse de verdad: si no, la guardia no está mirando su superficie");
        cited.Should().Contain("Node_Param_OpenPresetManager");
        cited.Should().Contain("PasswordManager_HeaderTitle",
            "la superficie del GESTOR DE CONTRASEÑAS también se lee del diccionario: si sus claves se caen del "
            + "censo, su cuerpo quedaría con los textos incrustados y sin cambiar de idioma");
        cited.Should().Contain("Node_Param_OpenPasswordManager");
        cited.Should().Contain("ToggleParametersToolTip",
            "el conmutador de parámetros de la tarjeta es la puerta del panel donde vive la acción «🎬 Presets...»: "
            + "si su rótulo se cae del censo, el host la pintaría con el texto incrustado");

        // Cada familia se contrasta contra el diccionario que la DECLARA —y en los dos idiomas—: las del host en
        // los del host; las de la superficie del plugin en los de su plugin, que son su única fuente (el cargador
        // los registra al cargar el ensamblado).
        var pluginDictionaries = PluginFamilies.ToDictionary(
            family => family.Family,
            family => (English: HostDictionaries.Of($"{family.Plugin}/Resources/Strings.resx"),
                       Spanish: HostDictionaries.Of($"{family.Plugin}/Resources/Strings.es.resx")));

        var orphan = new List<string>();
        foreach (string key in cited)
        {
            string? family = PluginFamilies
                .Select(candidate => candidate.Family)
                .FirstOrDefault(prefix => key.StartsWith(prefix, StringComparison.Ordinal));
            var (ownerEnglish, ownerSpanish) = family is null ? (english, spanish) : pluginDictionaries[family];

            if (!ownerEnglish.ContainsKey(key) || !ownerSpanish.ContainsKey(key))
            {
                orphan.Add(family is null ? $"{key} (diccionario del host)" : $"{key} ({family}* del plugin)");
            }
        }

        orphan.Should().BeEmpty(
            "sin entrada en el diccionario que declara la familia —y en sus dos idiomas— GetString resuelve el "
            + "fallback incrustado y cambiar de idioma deja la mitad del modal en el idioma equivocado");

        // Y cada par de diccionarios declara las MISMAS claves: un idioma al que le falte una entrada cae al
        // valor neutro del otro.
        var pairs = new List<(string Label, Dictionary<string, string> English, Dictionary<string, string> Spanish)>
        {
            ("el diccionario del host", english, spanish),
        };
        pairs.AddRange(PluginFamilies.Select(family => ($"el diccionario de {family.Plugin}",
            pluginDictionaries[family.Family].English, pluginDictionaries[family.Family].Spanish)));

        foreach (var (label, pairEnglish, pairSpanish) in pairs)
        {
            pairEnglish.Keys.OrderBy(k => k, StringComparer.Ordinal)
                .Should().Equal(pairSpanish.Keys.OrderBy(k => k, StringComparer.Ordinal),
                    $"{label} declara las mismas claves en los dos idiomas");
        }

        // Y el host NO vuelve a copiar esas familias: la copia sería la TERCERA fuente —la que tapa a la del
        // plugin— y es exactamente la divergencia que este tramo cerró.
        english.Keys.Should().NotContain(
            key => PluginFamilies.Any(family => key.StartsWith(family.Family, StringComparison.Ordinal)),
            "cada familia vive en el diccionario de su plugin, que el cargador registra: una copia en el host la "
            + "tapa por orden de registro y vuelve a abrir la puerta a que las dos digan cosas distintas");
        spanish.Keys.Should().NotContain(
            key => PluginFamilies.Any(family => key.StartsWith(family.Family, StringComparison.Ordinal)),
            "lo mismo en el otro idioma");
    }
}
