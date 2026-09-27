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
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

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

            var mainVm = services.GetRequiredService<MainViewModel>();            Canvas.Editor = mainVm.Editor;
            TryLoadSampleFlow(mainVm.Editor);

            // Rebanada 4: los dos paneles del editor, consumiendo los VM del núcleo (los mismos que
            // el escritorio): el cajón añade nodos al MISMO editor que pinta el lienzo, el inspector
            // sigue la selección que el lienzo escribe.
            Toolbox.Vm = mainVm.Toolbox;
            Toolbox.Editor = mainVm.Editor;
            Inspector.Vm = mainVm.NodeInspector;

            var loc = LocalizationManager.Instance;
            var core = loc.GetFormattedString(
                "Uno_HostCoreReady",
                "Núcleo portable listo: {0} nodos, MainViewModel resuelto.",
                nodes);

            Console.WriteLine("[UnoHost] nodos descubiertos: " + nodes);

            int catalogue = mainVm.Toolbox.CategoryGroups.SelectMany(g => g.Items).Count();
            engineStatus.Text = core
                + Environment.NewLine
                + loc.GetFormattedString(
                "Uno_HostViewModel",
                "Lienzo montado: {0} nodos en el grafo.",
                mainVm.Editor.Nodes.Count)
                + Environment.NewLine
                + loc.GetFormattedString(
                "Uno_HostPanels",
                "Paneles montados: cajón con {0} tipos de nodo, inspector conectado a la selección.",
                catalogue);

            Title = "FileFlow Studio — Uno Platform";

            BuildStatusBar(mainVm);

            // Localización en caliente (fase 3.5): los textos del marco se rescriben al cambiar el idioma.
            // El lienzo ya reconstruye los suyos al reasignar Editor (el selector de idioma vive en los
            // ajustes del escritorio; cuando el núcleo cambie la cultura, LanguageChanged notifica).
            LocalizationManager.Instance.LanguageChanged += (_, _) => RefreshLocalizedTexts(
                nodes, mainVm.Editor.Nodes.Count,
                mainVm.Toolbox.CategoryGroups.SelectMany(g => g.Items).Count());
        }
        catch (Exception ex)
        {
            engineStatus.Text = $"Fallo al arrancar el núcleo portable: {ex.Message}";
        }
    }

    /// <summary>
    /// El botón Ejecutar (hito 243): el comando canónico del ControlBar del núcleo — el MISMO que
    /// el botón del escritorio. El guion UIA externo lo invoca por su AutomationId para el ciclo
    /// completo (ejecutar → snapshot nuevo → diff recalculado). La barra de estado expone el
    /// estado de ejecución y los contadores de snapshots/diff del nodo fuente: el legible del
    /// ciclo para un observador sin acceso al árbol de VMs.
    /// </summary>
    private void BuildStatusBar(MainViewModel mainVm)
    {
        var loc = LocalizationManager.Instance;
        var controlBar = mainVm.ControlBar;

        var runButton = new Button
        {
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = loc.GetString("Uno_RunExecute", "Ejecutar")
        };
        AutomationProperties.SetAutomationId(runButton, "ExecuteButton");
        int clicksReceived = 0;
        runButton.Click += async (_, _) =>
        {
            // La marca del clic (latch del canal): distingue «el botón no recibió el gesto» de
            // «el comando corrió y fue rechazado por el diagnóstico» — ambas acaban en idle.
            clicksReceived++;
            engineStatus.Text = StatusLineWriter.Padded($"run: click #{clicksReceived} recibido");
            try
            {
                if (controlBar.ExecuteWorkflowCommand.CanExecute(null))
                {
                    await controlBar.ExecuteWorkflowCommand.ExecuteAsync(null);
                }
                else
                {
                    engineStatus.Text = StatusLineWriter.Padded("run: RECHAZADO por CanExecute (IsRunning="
                        + controlBar.IsRunning + ")");
                }
            }
            catch (Exception ex)
            {
                // El veredicto del ciclo no puede morir en silencio: la excepción del comando
                // queda en la línea del canal para el observador externo.
                engineStatus.Text = StatusLineWriter.Padded("run: EXCEPCION " + ex.GetType().Name
                    + ": " + ex.Message);
            }
        };

        controlBar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ControlBarViewModel.IsRunning) or nameof(ControlBarViewModel.IsDryRun))
            {
                RefreshExecutionStatus(controlBar);
            }
        };

        StatusBarHost.Children.Add(runButton);
        RefreshExecutionStatus(controlBar);
    }

    /// <summary>La línea legible del ciclo: estado del ControlBar y contadores del nodo fuente.</summary>
    private void RefreshExecutionStatus(ControlBarViewModel controlBar)
    {
        var source = controlBar.Editor.Nodes.FirstOrDefault(n => n.Title.Contains("Source", StringComparison.OrdinalIgnoreCase))
                     ?? controlBar.Editor.Nodes.FirstOrDefault();
        var inspector = controlBar.NodeInspector;
        string counters = source is null
            ? "sin grafo"
            : $"node={source.Title} snapshots={source.InputSnapshots.Count + source.OutputSnapshots.Count} diff={inspector.MetadataDiffs.Count}";
        string line = $"run: {(controlBar.IsRunning ? (controlBar.IsDryRun ? "dry-run" : "running") : "idle")} | "
            + counters;

        // El renglón padded es el CANAL del ciclo (hito 243): la UI lo pinta y un observador
        // externo (el guion UIA del ciclo completo) lo lee atómicamente por el writer.
        engineStatus.Text = StatusLineWriter.Padded(line);
    }

    /// <summary>El renglón del ciclo para lectores externos (la sonda de la superficie UIA).</summary>
    public static string ExecutionStatusLine => StatusLineWriter.Current;

    /// <summary>Los textos localizados del marco del host, re-escritura del idioma vigente.</summary>
    private void RefreshLocalizedTexts(int nodes, int canvasNodes, int catalogue)
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
            canvasNodes)
            + Environment.NewLine
            + loc.GetFormattedString(
            "Uno_HostPanels",
            "Paneles montados: cajón con {0} tipos de nodo, inspector conectado a la selección.",
            catalogue);
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
