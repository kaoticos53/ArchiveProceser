using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using FileFlow.App;
using FileFlow.App.Services;
using FileFlow.App.Views;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Prueba de humo del <b>arranque completo</b>: la red que faltaba entre «el código compila» y «la aplicación
/// se abre».
///
/// Las demás pruebas de vistas comprueban piezas por separado (una captura, un binding, un token). Un fallo
/// global del arranque —un XAML que no carga, un panel que deja de estar en el árbol, un contenedor de
/// servicios que ya no resuelve, un catálogo de nodos duplicado— pasaba por delante de todas ellas y sólo se
/// veía al ejecutar la aplicación a mano. Eso es exactamente lo que ocurrió cuando la ventana dejó de
/// aparecer: el proceso arrancaba, el XAML lanzaba y no quedaba ni ventana ni explicación.
///
/// Aquí se monta el shell real (la <c>MainWindow</c> de producción con su XAML, sobre los view models reales
/// alimentados con dobles de sus puertos) y se exige que <b>aparezca y se pinte</b>, con cada panel en su
/// sitio y el catálogo de nodos descubierto de verdad.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class StartupSmokeTests : IClassFixture<SharedAppVisualFixture>
{
    private readonly SharedAppVisualFixture _shared;

    public StartupSmokeTests(SharedAppVisualFixture shared) => _shared = shared;

    [Fact]
    public void TheShell_ShouldBootAndRenderEveryPanel_WithoutThrowing()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var window = new MainWindow(fixture.Shell);
            window.Show();

            try
            {
                // La ventana existe y está enlazada al shell.
                window.IsVisible.Should().BeTrue("el arranque debe terminar con una ventana visible");
                window.DataContext.Should().BeSameAs(fixture.Shell);

                // Cada panel está en el árbol una sola vez, enlazado a su view model: un panel que desaparece
                // del XAML deja de ser funcionalidad sin fallar ninguna otra prueba.
                List<ILogical> logical = [.. window.GetLogicalDescendants()];

                logical.OfType<ControlBarView>().Should().ContainSingle();
                logical.OfType<NodeToolboxView>().Should().ContainSingle();
                logical.OfType<EditorView>().Should().ContainSingle();
                logical.OfType<NodeInspectorPanelView>().Should().ContainSingle();
                logical.OfType<LogView>().Should().ContainSingle();
                logical.OfType<StatusBarView>().Should().ContainSingle();

                logical.OfType<ControlBarView>().Single().DataContext.Should().BeSameAs(fixture.ControlBar);
                logical.OfType<NodeToolboxView>().Single().DataContext.Should().BeSameAs(fixture.Toolbox);
                logical.OfType<EditorView>().Single().DataContext.Should().BeSameAs(fixture.Editor);
                logical.OfType<NodeInspectorPanelView>().Single().DataContext.Should().BeSameAs(fixture.Inspector);
                logical.OfType<LogView>().Single().DataContext.Should().BeSameAs(fixture.LogConsole);
                logical.OfType<StatusBarView>().Single().DataContext.Should().BeSameAs(fixture.StatusBar);

                // Y el shell se pinta de verdad: es la comprobación que distingue «el XAML cargó» de «la
                // ventana existe pero está en blanco».
                var frame = window.CaptureRenderedFrame();
                frame.Should().NotBeNull("un arranque correcto debe producir un fotograma real");
                frame!.PixelSize.Width.Should().BeGreaterThan(0);
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void ThePluginCatalog_ShouldBeDiscovered_ThroughTheLoaderTheStartupUses()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            // Es la etapa «carga de plugins» del arranque: si el descubrimiento se rompe, la caja de
            // herramientas aparece vacía y no hay nada que arrastrar al lienzo.
            var loader = PluginRegistryHelper.CreateConfiguredLoader();

            loader.UniqueNodeTypes.Should().NotBeEmpty("el arranque debe descubrir el catálogo de nodos");
            loader.UniqueNodeTypes
                .Select(t => t.FullName)
                .Should().OnlyHaveUniqueItems("un tipo de nodo no puede registrarse dos veces");
        });
    }

    [Fact]
    public void TheToolbox_ShouldListEveryNodeTypeExactlyOnce()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var items = fixture.Toolbox.CategoryGroups.SelectMany(g => g.Items).ToList();

            items.Should().HaveCountGreaterThan(10, "el catálogo real de nodos debe llegar a la caja de herramientas");

            // Regresión histórica: el catálogo se duplicaba en pantalla 1-2 s después del arranque.
            items.Select(i => i.TypeName).Should().OnlyHaveUniqueItems("ningún nodo puede aparecer dos veces");
        });
    }

    [Fact]
    public void TheToolbox_ShouldExposeEveryCategoryExactlyOnce()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var groups = fixture.Toolbox.CategoryGroups.ToList();

            groups.Should().NotBeEmpty();
            groups.Select(g => g.CategoryKey).Should().OnlyHaveUniqueItems("las categorías no pueden repetirse");
        });
    }

    [Fact]
    public void TheSampleGraph_ShouldLoadWithTypedPorts()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var nodes = fixture.Editor.Nodes;

            nodes.Should().NotBeEmpty("el lienzo debe arrancar con un grafo sobre el que trabajar");
            foreach (var node in nodes)
            {
                node.NodeTypeName.Should().NotBeNullOrWhiteSpace();
                (node.InputPorts.Count + node.OutputPorts.Count).Should().BeGreaterThan(0,
                    $"'{node.NodeTypeName}' debe exponer puertos: sin ellos no se puede conectar nada");
            }
        });
    }
}
