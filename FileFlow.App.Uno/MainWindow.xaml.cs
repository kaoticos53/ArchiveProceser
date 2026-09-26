using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// Ventana principal del host Uno. Fase 3.1: el lienzo del editor monta el <see cref="EditorViewModel"/>
/// del núcleo portable y carga el flujo de ejemplo del banco si está disponible en disco — nodos y cables
/// a sus posiciones, pintados con la geometría compartida. La barra inferior conserva la prueba de vida
/// del núcleo de la rebanada 1.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            var services = App.Services;

            var loader = services.GetRequiredService<PluginLoader>();
            int nodes = loader.DiscoveredNodesCount;

            var mainVm = services.GetRequiredService<MainViewModel>();

            Canvas.Editor = mainVm.Editor;
            TryLoadSampleFlow(mainVm.Editor);

            var loc = LocalizationManager.Instance;
            var core = loc.GetFormattedString(
                "Uno_HostCoreReady",
                "Núcleo portable listo: {0} nodos, MainViewModel resuelto.",
                nodes);

            Console.WriteLine("[UnoHost] nodos descubiertos: " + nodes);

            engineStatus.Text = core
                + Environment.NewLine
                + loc.GetFormattedString(
                    "Uno_HostViewModel",
                    "Lienzo montado: {0} nodos en el grafo.",
                    mainVm.Editor.Nodes.Count);

            Title = "FileFlow Studio — Uno Platform";

            // Localización en caliente (fase 3.5): los textos del marco se rescriben al cambiar el idioma.
            // El lienzo ya reconstruye los suyos al reasignar Editor (el selector de idioma vive en los
            // ajustes del escritorio; cuando el núcleo cambie la cultura, LanguageChanged notifica).
            LocalizationManager.Instance.LanguageChanged += (_, _) => RefreshLocalizedTexts(nodes, mainVm.Editor.Nodes.Count);
        }
        catch (Exception ex)
        {
            engineStatus.Text = $"Fallo al arrancar el núcleo portable: {ex.Message}";
        }
    }

    /// <summary>Los dos textos localizados del marco del host, re-escritura del idioma vigente.</summary>
    private void RefreshLocalizedTexts(int nodes, int canvasNodes)
    {
        var loc = LocalizationManager.Instance;
        engineStatus.Text = loc.GetFormattedString(
            "Uno_HostCoreReady",
            "Núcleo portable listo: {0} nodos, MainViewModel resuelto.",
            nodes)
            + Environment.NewLine
            + loc.GetFormattedString(
            "Uno_HostViewModel",
            "Lienzo montado: {0} nodos en el grafo.",
            canvasNodes);
    }

    /// <summary>El error del último intento de carga del ejemplo (vacío si no hubo): visible para el sondeo.</summary>
    public string? SampleLoadError { get; private set; }

    /// <summary>
    /// Carga el primer flujo de ejemplo que encuentre en las carpetas canónicas del producto, para que el
    /// lienzo muestre un grafo real en el arranque. Sin ejemplos en disco, el lienzo arranca vacío; un
    /// error de carga no impide el arranque pero SE REPORTA (consola y <see cref="SampleLoadError"/>).
    /// </summary>
    private void TryLoadSampleFlow(EditorViewModel editor)
    {
        // Del bin del host al repositorio: caminar hacia arriba hasta un directorio que contenga
        // docs\examples (el marcador del banco). En instalación publicada, la copia local Examples/ manda.
        string? repoRoot = AppContext.BaseDirectory;
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot, "docs", "examples")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot.TrimEnd(Path.DirectorySeparatorChar));
        }

        // Sin rastro paso a paso (BaseDirectory, candidatos, primer json): el RESULTADO informa y el
        // error, si lo hay, se reporta. La barra de estado de la ventana ya dice cuántos nodos quedaron.
        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "Examples"),
                     repoRoot is null ? "" : Path.Combine(repoRoot, "docs", "examples")
                 })
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var first = Directory.EnumerateFiles(candidate, "*.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (first is not null)
            {
                try
                {
                    var json = File.ReadAllText(first);
                    var graph = FileFlow.Core.Engine.WorkflowGraph.FromJson(json);
                    editor.LoadFromGraphModel(graph);
                    Console.WriteLine("[UnoHost] ejemplo cargado: " + graph.Nodes.Count + " nodos ("
                        + Path.GetFileName(first) + ")");
                }
                catch (Exception ex)
                {
                    // Un ejemplo que no se puede leer no impide el arranque, pero el error no se traga:
                    // queda visible en consola y para el sondeo en runtime (--selfcheck).
                    SampleLoadError = ex.GetType().Name + ": " + ex.Message
                        + (ex.InnerException is null ? "" : " | " + ex.InnerException.Message);
                    Console.Error.WriteLine("[TryLoadSampleFlow] " + SampleLoadError);
                }

                return;
            }
        }
    }
}
