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
/// La guardia de los <b>TEXTOS</b> del menú principal del host Uno (hito 257): las cadenas que la barra y su
/// cajón enseñan son las del ESCRITORIO, copiadas clave por clave en los dos idiomas.
///
/// <para><b>Por qué es una guardia aparte</b>: el sujeto no es lo que la barra hace sino lo que dice, y su
/// referencia no es el código del núcleo sino los diccionarios del escritorio. Una traducción propia del host
/// sería otra interfaz —el usuario vería dos productos— y un texto sin entrada en el diccionario sale en la
/// interfaz como su clave.</para>
///
/// <para>Los diccionarios los lee <c>HostDictionaries</c>, que comparten las guardias de textos del host.</para>
/// </summary>
public class UnoControlBarTextsGuardTests
{
    private const string BarCode = "FileFlow.App.Uno/Controls/ControlBar.xaml.cs";
    private const string DrawerCode = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml.cs";
    private const string StringsEnglish = "FileFlow.App.Uno/Resources/Strings.resx";
    private const string StringsSpanish = "FileFlow.App.Uno/Resources/Strings.es.resx";

    /// <summary>La barra del escritorio y su cajón: la referencia de paridad.</summary>
    private const string DesktopStringsEnglish = "FileFlow.App.Core/Resources/Strings.resx";
    private const string DesktopStringsSpanish = "FileFlow.App.Core/Resources/Strings.es.resx";

    /// <summary>Dónde vive la orden de una entrada: en el code-behind (un comando) o en el XAML (un enlace).</summary>
    private enum Home
    {
        Code,
        Xaml,
    }

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. Los textos son los del escritorio, en los dos idiomas
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>De la clave del host a la clave del diccionario del escritorio: el texto se COPIA, no se re-traduce.</summary>
    private static IReadOnlyList<(string Host, string Desktop)> SharedTexts() =>
    [
        ("Uno_ControlBar_Menu", "MenuBtn"),
        ("Uno_ControlBar_MenuToolTip", "ControlBar_MenuToolTip"),
        ("Uno_ControlBar_DryRun", "DryRun"),
        ("Uno_ControlBar_DryRunToolTip", "ControlBar_DryRunToolTip"),
        ("Uno_ControlBar_Watcher", "ControlBar_Watcher"),
        ("Uno_ControlBar_WatchToolTip", "ControlBar_WatchModeToolTip"),
        ("Uno_ControlBar_Run", "RunFlow"),
        ("Uno_ControlBar_Debug", "DebugFlow"),
        ("Uno_ControlBar_StepNext", "StepNext"),
        ("Uno_ControlBar_StepNextToolTip", "ControlBar_StepNextToolTip"),
        ("Uno_ControlBar_Continue", "ContinueFlow"),
        ("Uno_ControlBar_ContinueToolTip", "ControlBar_ContinueToolTip"),
        ("Uno_ControlBar_Pause", "Pause"),
        ("Uno_ControlBar_PauseToolTip", "ControlBar_PauseToolTip"),
        ("Uno_ControlBar_Stop", "Stop"),
        ("Uno_ControlBar_StopToolTip", "ControlBar_StopToolTip"),
        ("Uno_ControlBar_Undo", "UndoBtn"),
        ("Uno_ControlBar_UndoToolTip", "UndoToolTip"),
        ("Uno_ControlBar_Redo", "RedoBtn"),
        ("Uno_ControlBar_RedoToolTip", "RedoToolTip"),
        ("Uno_ControlBar_Rollback", "RollbackExecutionBtn"),
        ("Uno_ControlBar_RollbackToolTip", "RollbackExecutionToolTip"),
        ("Uno_ControlBar_Inspector", "InspectorBtn"),
        ("Uno_ControlBar_InspectorToolTip", "ControlBar_InspectorToolTip"),
        ("Uno_Drawer_Subtitle", "Drawer_AppSubtitle"),
        ("Uno_Drawer_AppearanceLanguage", "Drawer_AppearanceLanguage"),
        ("Uno_Drawer_ThemeLabel", "Drawer_ThemeLabel"),
        ("Uno_Drawer_LanguageLabel", "Drawer_LanguageLabel"),
        ("Uno_Drawer_PanelsTools", "Drawer_PanelsTools"),
        ("Uno_Drawer_Settings", "Drawer_Settings"),

        // Hito 258 — las secciones y entradas nuevas del cajón, y la ventana «Acerca de».
        ("Uno_Drawer_FlowManagement", "Drawer_FlowManagement"),
        ("Uno_Drawer_NewWorkflow", "Drawer_NewWorkflow"),
        ("Uno_Drawer_LoadWorkflow", "Drawer_LoadWorkflow"),
        ("Uno_Drawer_SaveWorkflow", "Drawer_SaveWorkflow"),
        ("Uno_Drawer_HelpResources", "Drawer_HelpResources"),
        ("Uno_Drawer_UserManual", "Drawer_UserManual"),
        ("Uno_Drawer_UserManualToolTip", "Drawer_UserManualToolTip"),
        ("Uno_Drawer_ExampleFlows", "Drawer_ExampleFlows"),
        ("Uno_Drawer_ExampleFlowsToolTip", "Drawer_ExampleFlowsToolTip"),
        ("Uno_Drawer_About", "Drawer_About"),
        ("Uno_Drawer_AboutToolTip", "Drawer_AboutToolTip"),
        ("Uno_About_Title", "About_Title"),
        ("Uno_About_Subtitle", "About_Subtitle"),
        ("Uno_About_Description", "About_Description"),
        ("Uno_About_Accept", "Common_Accept"),
    ];

    [Fact]
    public void TheSharedTexts_ShouldBeTheDesktopOnes_InBothLanguages()
    {
        foreach (var (hostPath, desktopPath) in new[]
                 {
                     (StringsEnglish, DesktopStringsEnglish),
                     (StringsSpanish, DesktopStringsSpanish),
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
                "los textos de la barra y del cajón son los del escritorio, copiados: una traducción propia "
                + $"sería otra interfaz ({Path.GetFileName(hostPath)})");
        }
    }

    [Fact]
    public void EveryTextUsedByTheViews_ShouldExistInBothHostDictionaries()
    {
        var english = HostDictionaries.Of(StringsEnglish);
        var spanish = HostDictionaries.Of(StringsSpanish);
        string views = Code(BarCode) + Code(DrawerCode);

        var cited = Regex.Matches(views, @"""(Uno_[A-Za-z_]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        cited.Should().NotBeEmpty("las vistas de la barra citan sus claves del diccionario del host");

        var missing = cited.Where(key => !english.ContainsKey(key) || !spanish.ContainsKey(key)).ToList();
        missing.Should().BeEmpty(
            "sin entrada en los dos diccionarios, GetString resuelve el fallback incrustado y el cambio de "
            + "idioma deja la mitad del marco en el idioma equivocado");

        english.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Should().Equal(spanish.Keys.OrderBy(k => k, StringComparer.Ordinal),
                "los dos diccionarios del host declaran las mismas claves");
    }
}
