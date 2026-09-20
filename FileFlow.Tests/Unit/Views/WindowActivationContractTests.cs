using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Plugin.FileSystem.UI.Views;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardias del <b>contrato de activación</b> de las ventanas: una ventana cuyos enlaces son su única fuente de
/// contenido no puede abrirse sin view model.
///
/// El diseñador de datos sintéticos se abría así —`new SyntheticDataSetDesignerWindow()` en el nodo de origen y
/// en el renamer avanzado— y el síntoma era el de siempre: la ventana aparecía, pero **sin datos, sin campos
/// editables y con todos los botones mudos** (los comandos eran `null`). No fallaba nada: un `{Binding}` sin
/// `DataContext` simplemente no resuelve.
///
/// La guardia tiene dos mitades: la ventana <b>se defiende sola</b> (prueba de comportamiento, abriéndola sin
/// view model) y <b>ninguna ventana del repositorio</b> puede construirse sin argumentos si no garantiza su
/// `DataContext` con la misma red de seguridad (lint sobre el código de apertura).
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class WindowActivationContractTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 1. La ventana del diseñador se defiende sola
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheDataSetDesigner_ShouldActivateWithAWorkingViewModel_WhenOpenedWithoutOne()
    {
        // Es el camino real de la aplicación: `new SyntheticDataSetDesignerWindow()`, sin DataContext.
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = new SyntheticDataSetDesignerWindow();
            window.Show();

            try
            {
                Dispatcher.UIThread.RunJobs();

                var vm = window.ViewModel;
                vm.Should().NotBeNull(
                    "la ventana debe crear su view model al abrirse: sin él no hay datos ni un solo botón que actúe");

                vm!.FilteredDataSets.Should().NotBeEmpty(
                    "el catálogo debe mostrar los datasets oficiales (derivados de las muestras embebidas)");
                vm.FilteredDataSets.Should().Contain(d => d.IsBuiltIn, "los datasets oficiales son la entrada por defecto");

                vm.SelectedDataSet.Should().NotBeNull("la primera entrada del catálogo queda seleccionada");
                vm.EditableItems.Should().NotBeEmpty("el dataset seleccionado debe cargar sus elementos");
                vm.HasSelectedDataSet.Should().BeTrue();

                foreach (var (name, command) in new (string, object?)[]
                         {
                             ("Guardar", vm.SaveCommand),
                             ("Nuevo", vm.NewDataSetCommand),
                             ("Duplicar", vm.DuplicateDataSetCommand),
                             ("Eliminar dataset", vm.DeleteDataSetCommand),
                             ("Añadir archivo", vm.AddFileCommand),
                             ("Añadir carpeta", vm.AddFolderCommand),
                             ("Añadir comprimido", vm.AddArchiveToTreeCommand),
                             ("Quitar nodo", vm.RemoveItemCommand)
                         })
                {
                    command.Should().NotBeNull($"el botón «{name}» sin comando es un botón que no hace nada");
                }
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void TheDataSetDesigner_ShouldRenderItsEditorAgainstTheInjectedViewModel()
    {
        // Con view model inyectado, la ventana debe pintar el editor completo: cabecera, catálogo, árbol con
        // nodos, inspector y pestañas con sus cabeceras traducidas. Una ventana «vacía pero abierta» falla aquí.
        string storageDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_DesignerContract_" + Guid.NewGuid().ToString("N"));

        AvaloniaTestHelper.RunOnUI(() =>
        {
            try
            {
                var vm = new SyntheticDataSetDesignerViewModel(
                    new SyntheticDataSetStorageService(storageDirectory),
                    NullDialogService.Instance);

                var window = new SyntheticDataSetDesignerWindow(vm);
                window.Show();

                try
                {
                    Dispatcher.UIThread.RunJobs();

                    window.ViewModel.Should().BeSameAs(vm, "la ventana no debe pisar el view model inyectado");

                    var texts = window.GetLogicalDescendants()
                        .OfType<TextBlock>()
                        .Select(t => t.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    texts.Should().NotBeEmpty("el editor debe renderizar texto");

                    foreach (string key in new[]
                             {
                                 "DataSetDesigner_HeaderTitle",
                                 "DataSetDesigner_CatalogTitle",
                                 "DataSetDesigner_PropertiesTitle",
                                 "DataSetDesigner_InspectorTitle"
                             })
                    {
                        string expected = LocalizationManager.Instance[key];
                        expected.Should().NotBeNullOrWhiteSpace($"la clave '{key}' debe existir");

                        texts.Should().Contain(expected,
                            $"la ventana debe pintar la sección '{key}' con el idioma activo");
                    }

                    // 'Distinct': el recorrido lógico de Avalonia devuelve dos veces el contenido del 'TabItem'
                    // seleccionado (misma instancia), y un 'Single' sobre las repeticiones da un falso positivo.
                    var tree = window.GetLogicalDescendants().OfType<TreeView>().Distinct().SingleOrDefault();
                    tree.Should().NotBeNull("el diseñador se define por su árbol jerárquico editable");
                    tree!.ItemsSource.Should().NotBeNull("el árbol debe estar alimentado por el view model");

                    var tabs = window.GetLogicalDescendants().OfType<TabControl>().SingleOrDefault();
                    tabs.Should().NotBeNull();
                    tabs!.Items.OfType<TabItem>().Select(t => t.Header?.ToString()).Should().BeEquivalentTo(
                        [
                            LocalizationManager.Instance["DataSetDesigner_TabTree"],
                            LocalizationManager.Instance["DataSetDesigner_TabDsl"],
                            LocalizationManager.Instance["DataSetDesigner_TabJson"]
                        ],
                        "las tres vistas del dataset (árbol, DSL y JSON) deben existir y estar traducidas");
                }
                finally
                {
                    VisualSnapshot.DetachTree(window);
                    window.Close();
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(storageDirectory))
                    {
                        Directory.Delete(storageDirectory, recursive: true);
                    }
                }
                catch
                {
                    // Limpieza best-effort de un directorio temporal de la prueba.
                }
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. Ninguna ventana se abre sin garantizar su view model
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryWindowBuiltWithoutArguments_ShouldProvideItsOwnDataContext()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var offenders = new List<string>();
        var checkedWindows = new List<string>();

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !IsBuildArtifact(p) && !p.Contains("Tests", StringComparison.OrdinalIgnoreCase)))
        {
            string code = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(code, @"new\s+([A-Za-z0-9_]+Window)\s*(?:\(\s*\)|\{)"))
            {
                string windowType = match.Groups[1].Value;

                string? view = Directory
                    .EnumerateFiles(root, windowType + ".axaml", SearchOption.AllDirectories)
                    .FirstOrDefault(p => !IsBuildArtifact(p));

                if (view == null)
                {
                    continue; // ventana de una librería externa o sin XAML propio
                }

                if (!DependsOnDataContext(File.ReadAllText(view)))
                {
                    continue; // sin enlaces contra el DataContext, la ventana no lo necesita
                }

                checkedWindows.Add(windowType);

                string? codeBehind = Directory
                    .EnumerateFiles(Path.GetDirectoryName(view)!, windowType + ".axaml.cs", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (codeBehind == null)
                {
                    offenders.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}: new {windowType}() sin code-behind");
                    continue;
                }

                string codeBehindText = File.ReadAllText(codeBehind);

                // Dos formas válidas de hacerse cargo del DataContext en el camino sin argumentos:
                //   a) resolverlo al abrirse (`OnOpened` + `DataContext = new …`), la red de seguridad del diseñador;
                //   b) que el constructor sin parámetros delegue (`: this(…)`) en otro que lo asigne, y que la clase
                //      lo asigne en algún sitio (la ventana principal resuelve su view model del contenedor así).
                // Lo que no vale —y era el fallo del diseñador— es un constructor sin parámetros que no delega,
                // con la única asignación en la sobrecarga que nadie llama: se abre vacía y sin botones.
                bool guaranteesOnOpen =
                    codeBehindText.Contains("OnOpened", StringComparison.Ordinal) &&
                    codeBehindText.Contains("DataContext = new", StringComparison.Ordinal);

                // El tramo hasta la llave se acota con '[^{]' a propósito: con '\s\S' el tramo cruzaba el cuerpo
                // del primer constructor y llegaba hasta un ': this(…)' de OTRA sobrecarga, de modo que el lint daba
                // por buena una ventana rota (falso negativo que cazó la comprobación por mutación).
                bool delegatesAndAssigns = false;
                string parameterlessCtor = @"public\s+" + windowType + @"\s*\(\s*\)\s*(?<tail>[^{]{0,200}?)\s*\{";

                foreach (Match ctor in Regex.Matches(codeBehindText, parameterlessCtor))
                {
                    if (ctor.Groups["tail"].Value.Contains(": this(", StringComparison.Ordinal))
                    {
                        delegatesAndAssigns = codeBehindText.Contains("DataContext =", StringComparison.Ordinal);
                        break;
                    }
                }

                if (!guaranteesOnOpen && !delegatesAndAssigns)
                {
                    offenders.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}: new {windowType}()");
                }
            }
        }

        checkedWindows.Distinct().Count().Should().BeGreaterThan(
            1,
            "el lint debe estar viendo las ventanas que la aplicación construye sin argumentos");

        offenders.Should().BeEmpty(
            "una ventana con enlaces que se construye sin argumentos debe garantizar su propio DataContext al " +
            "abrirse (OnOpened + `DataContext = new …`): sin él se abre vacía y con los botones mudos, que es " +
            "exactamente cómo se rompió el diseñador de datos sintéticos. Revisa: " + string.Join(" | ", offenders));
    }

    /// <summary>
    /// ¿La vista resuelve algún enlace contra su <c>DataContext</c>?
    ///
    /// No basta con «contiene «{Binding»»: una ventana escrita contra controles nombrados (como el gestor de presets de
    /// media) enlaza textos localizados con <c>Source=</c> explícito y sólo usa el contexto de plantilla para los
    /// elementos de una lista, así que se abre perfectamente sin <c>DataContext</c> y no debe entrar en el lint. Lo que
    /// sí depende del contexto es un enlace sin origen explícito —ni <c>Source=</c>, ni <c>$parent</c>, ni
    /// <c>RelativeSource</c>— que además no viva dentro de una plantilla de elementos (allí el contexto es el elemento).
    /// </summary>
    private static bool DependsOnDataContext(string xaml)
    {
        string withoutTemplates = StripItemTemplates(xaml);

        return Regex.Matches(withoutTemplates, @"\{Binding\b[^{}]*\}")
            .Any(m => !m.Value.Contains("Source=", StringComparison.Ordinal) &&
                      !m.Value.Contains("$parent", StringComparison.Ordinal) &&
                      !m.Value.Contains("$self", StringComparison.Ordinal) &&
                      !m.Value.Contains("RelativeSource", StringComparison.Ordinal));
    }

    /// <summary>Vacía el contenido de las plantillas de elementos, donde el contexto es el elemento y no la ventana.</summary>
    private static string StripItemTemplates(string xaml)
    {
        var pattern = new Regex(@"<(?:Tree)?DataTemplate\b[^>]*>[\s\S]*?</(?:Tree)?DataTemplate>");
        string stripped = xaml;
        string previous;

        do
        {
            previous = stripped;
            stripped = pattern.Replace(stripped, string.Empty);
        }
        while (!string.Equals(stripped, previous, StringComparison.Ordinal));

        return stripped;
    }

    private static bool IsBuildArtifact(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
