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

            engineStatus.Text = core
                + Environment.NewLine
                + loc.GetFormattedString(
                    "Uno_HostViewModel",
                    "Lienzo montado: {0} nodos en el grafo.",
                    mainVm.Editor.Nodes.Count);

            Title = "FileFlow Studio — Uno Platform";
        }
        catch (Exception ex)
        {
            engineStatus.Text = $"Fallo al arrancar el núcleo portable: {ex.Message}";
        }
    }

    /// <summary>
    /// Carga el primer flujo de ejemplo que encuentre en las carpetas canónicas del producto, para que el
    /// lienzo muestre un grafo real en el arranque. Sin ejemplos en disco, el lienzo arranca vacío.
    /// </summary>
    private void TryLoadSampleFlow(EditorViewModel editor)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "Examples"),
                     Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "examples")
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
                }
                catch (Exception)
                {
                    // Un ejemplo que no se puede leer no impide el arranque: el lienzo queda vacío.
                }

                return;
            }
        }
    }
}
