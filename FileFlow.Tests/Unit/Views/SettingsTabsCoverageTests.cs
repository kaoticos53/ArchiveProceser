using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using FileFlow.Sdk.Localization;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardias de la ventana de ajustes: la <b>cobertura de secciones</b>, la <b>resolución de sus enlaces</b> y la
/// <b>existencia de cada clave de traducción</b> que la interfaz usa.
///
/// El defecto que motivó estas pruebas fue silencioso por partida doble:
/// <list type="number">
///   <item>la ventana declaraba tres pestañas cuando el view model mantenía preferencias para seis — apariencia,
///   rendimiento, herramientas externas y <b>modelos de IA</b> se guardaban y nadie podía editarlas;</item>
///   <item>la primera pestaña pedía <c>Path=[Settings_TabGeneral]</c>, una clave que no existe en ningún
///   diccionario. El enlazador del indexador devuelve cadena vacía ante una clave ausente (no es un fallo de
///   enlace), así que <c>FallbackValue</c> no entra en juego y la cabecera se pintaba en blanco.</item>
/// </list>
///
/// Con los enlaces compilados desactivados (<c>AvaloniaUseCompiledBindingsByDefault=false</c>) un camino que no
/// resuelve tampoco falla: el control queda mudo. Por eso las pruebas leen el XAML y validan cada camino contra
/// el tipo que declara <c>x:DataType</c>.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class SettingsTabsCoverageTests
{
    private const string Spanish = "es-ES";
    private const string English = "en-US";

    private const string SettingsXaml = "FileFlow.App/Views/Components/WorkflowSettingsWindow.axaml";
    private const string ManagerViewXaml = "FileFlow.App/Views/Components/AiModelManagerView.axaml";
    private const string DownloadDialogXaml = "FileFlow.App/Views/Components/AiModelDownloadDialog.axaml";

    /// <summary>Cabeceras del TabControl, en orden. Es el contrato de la ventana.</summary>
    private static readonly string[] ExpectedTabKeys =
    [
        "Settings_TabStorage",
        "Settings_TabAppearance",
        "Settings_TabPerformance",
        "Settings_TabExternalTools",
        "Settings_TabAiModels",
        "Settings_TabUpdates"
    ];

    /// <summary>
    /// Cada ajuste persistente y el enlace que lo edita. Una preferencia sin control es una preferencia que el
    /// usuario no puede cambiar: si se borra una pestaña, esta lista falla nombrándola.
    /// </summary>
    private static readonly string[] EditableSettingsBindings =
    [
        // Almacenamiento y rutas
        "GlobalOutputDir", "TempWorkingDir", "SelectedConflictStrategy",
        "EnableAutoSave", "AutoSaveIntervalMinutes",
        "AutoCleanIntermediateTempFiles", "CleanStaleTempOnStartup",
        // Apariencia e interfaz
        "SelectedLanguage", "SelectedTheme", "IsCompactToolbox", "AutoScrollConsole", "MaxLogEntries",
        // Rendimiento y ejecución
        "MaxParallelThreads", "DefaultDryRunState", "SelectedLogLevel", "EnableCheckpointing",
        "AutoUnloadAiModelsOnCompletion",
        // Herramientas externas
        "FfmpegPath", "FfprobePath", "SevenZipPath", "PythonPath",
        // Actualizaciones
        "AutoCheckForUpdates", "SelectedUpdateChannel",
        // Modelos de IA (el gestor completo, con su lista y sus descargas)
        "AiModelManager"
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. Pestañas: están todas y cada una se anuncia
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheSettingsWindow_ShouldExposeEverySectionAsATab()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = ModalVisualFixture.Build(ModalSurface.WorkflowSettings);
            window.Show();

            try
            {
                var tabs = window.FindControl<TabControl>("SettingsTabs");
                tabs.Should().NotBeNull("el TabControl de ajustes debe existir (es el ancla de las capturas y del contrato)");

                var headers = tabs!.Items.OfType<TabItem>().ToList();

                headers.Should().HaveCount(ExpectedTabKeys.Length,
                    "la ventana de ajustes debe ofrecer una pestaña por sección; faltar una deja preferencias " +
                    "que el usuario no puede editar (así desaparecieron rendimiento, herramientas externas y " +
                    "modelos de IA)");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void EverySettingsTabHeader_ShouldBeTranslatedInBothLanguagesWithoutPictographs()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            try
            {
                foreach (string culture in new[] { Spanish, English })
                {
                    AvaloniaTestHelper.SetCultureOnUI(culture);

                    var window = ModalVisualFixture.Build(ModalSurface.WorkflowSettings);
                    window.Show();

                    try
                    {
                        var headers = window.FindControl<TabControl>("SettingsTabs")!
                            .Items.OfType<TabItem>()
                            .ToList();

                        for (int i = 0; i < ExpectedTabKeys.Length && i < headers.Count; i++)
                        {
                            string expected = LocalizationManager.Instance[ExpectedTabKeys[i]];
                            string visible = VisibleHeaderText(headers[i]);

                            expected.Should().NotBeNullOrWhiteSpace(
                                $"la clave '{ExpectedTabKeys[i]}' debe existir: un enlazador contra una clave ausente " +
                                "devuelve cadena vacía (no un fallo de enlace) y la pestaña sale sin nombre");

                            visible.Should().Be(expected,
                                $"la pestaña {i} debe mostrar su cabecera traducida en '{culture}'");

                            visible.Any(IsPictograph).Should().BeFalse(
                                $"la cabecera '{visible}' no puede apoyarse en pictogramas: se ven distintos o como " +
                                "cuadraditos según el sistema; el icono de la pestaña es vectorial (MaterialIcon)");
                        }
                    }
                    finally
                    {
                        VisualSnapshot.DetachTree(window);
                        window.Close();
                    }
                }
            }
            finally
            {
                AvaloniaTestHelper.SetCultureOnUI(AvaloniaTestHelper.PinnedLanguage);
            }
        });
    }

    [Fact]
    public void EveryTabBody_ShouldRenderTheSectionItAnnounces()
    {
        // Una pestaña puede existir y estar vacía (contenido sin DataContext, enlaces que no resuelven):
        // el árbol lógico de cada TabItem se recorre buscando el texto testigo de su sección, resuelto de los
        // diccionarios con el idioma activo. Así, «la pestaña está» significa que además pinta lo que promete.
        string[] witnessKeys =
        [
            "Settings_GlobalOutputDirTitle",
            "Settings_DefaultThemeTitle",
            "Settings_ParallelCpuTitle",
            "Settings_FfmpegLabel",
            "AiModelManager_HeaderTitle",
            "Settings_UpdatesTitle"
        ];

        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = ModalVisualFixture.Build(ModalSurface.WorkflowSettings);
            window.Show();

            try
            {
                Dispatcher.UIThread.RunJobs();

                var tabs = window.FindControl<TabControl>("SettingsTabs")!.Items.OfType<TabItem>().ToList();
                tabs.Should().HaveCount(witnessKeys.Length);

                for (int i = 0; i < witnessKeys.Length; i++)
                {
                    string witness = LocalizationManager.Instance[witnessKeys[i]];
                    witness.Should().NotBeNullOrWhiteSpace($"la clave testigo '{witnessKeys[i]}' debe existir");

                    var rendered = tabs[i].GetLogicalDescendants()
                        .OfType<TextBlock>()
                        .Select(t => t.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    rendered.Should().NotBeEmpty($"el cuerpo de la pestaña {i} no puede estar vacío");
                    rendered.Should().Contain(witness,
                        $"el cuerpo de la pestaña {i} ({tabs[i].Header}) debe pintar su sección: el texto testigo " +
                        "'{0}' no aparece entre los {1} textos renderizados",
                        witnessKeys[i], rendered.Count);
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
    public void EverySettingsTabSurface_ShouldPaintItsBodyNotJustTheChrome()
    {
        // Evidencia por píxel de que cada cuerpo se pinta de verdad. La captura de una pestaña con el contenido
        // sin construir sale casi plana (sólo el marco), y eso no lo ve ninguna aserción sobre el XAML.
        (ModalSurface Surface, string Baseline)[] surfaces =
        [
            (ModalSurface.WorkflowSettings, "almacenamiento"),
            (ModalSurface.WorkflowSettingsAppearance, "apariencia"),
            (ModalSurface.WorkflowSettingsPerformance, "rendimiento"),
            (ModalSurface.WorkflowSettingsExternalTools, "herramientas externas"),
            (ModalSurface.WorkflowSettingsAiModels, "modelos de IA")
        ];

        var thin = new List<string>();

        foreach (var (surface, name) in surfaces)
        {
            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>
                (ModalVisualFixture.Capture(surface, "dark_fluent"));

            var histogram = new Dictionary<uint, int>();

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    uint key = ((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B;
                    histogram[key] = histogram.TryGetValue(key, out int seen) ? seen + 1 : 1;
                }
            }

            long total = (long)image.Width * image.Height;
            int background = histogram.Values.Max();
            double ink = (total - background) / (double)total;

            // Medido en las cinco pestañas: entre 1 685 y 2 478 colores y entre 32,7 % y 49,5 % de tinta. El umbral
            // queda muy por debajo de eso (y muy por encima de lo que pinta una ventana sin su cuerpo: sólo el
            // marco, las cabeceras y el pie dan unos pocos miles de píxeles, por debajo del 10 %).
            if (histogram.Count < 200 || ink < 0.15)
            {
                thin.Add($"{name}: {histogram.Count} colores, {(ink * 100):F1}% de tinta");
            }
        }

        thin.Should().BeEmpty(
            "cada pestaña debe pintar su sección (campos, etiquetas, la lista de modelos), no un marco vacío: " +
            string.Join(" | ", thin));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. Cada ajuste persistente tiene su control
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryPersistedSetting_ShouldBeEditableFromTheSettingsWindow()
    {
        string settings = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), SettingsXaml));

        var missing = EditableSettingsBindings
            .Where(binding => !Regex.IsMatch(settings, $@"\{{Binding\s+{Regex.Escape(binding)}(\s*[,}}])"))
            .ToList();

        missing.Should().BeEmpty(
            "cada preferencia persistente necesita un control en la ventana de ajustes; sin él se guarda en disco " +
            "y el usuario no puede cambiarla. Sin control: " + string.Join(", ", missing));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. Los enlaces del XAML resuelven contra su view model
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SettingsXaml, 20)]
    [InlineData(ManagerViewXaml, 20)]
    public void EveryBindingPathInTheSettingsUi_ShouldResolveAgainstItsDeclaredDataType(string relativePath, int minimumBindings)
    {
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath);
        XDocument document = XDocument.Load(path);

        // Los enlaces de plantilla se comprueban contra el tipo que declara el DataTemplate y los de lista
        // (SelectedValueBinding / DisplayMemberBinding) contra el elemento de la colección, que no se puede
        // deducir del XAML: esos dos quedan fuera del alcance estático.
        var offenders = new List<string>();
        int checkedPaths = 0;

        Type? rootType = ResolveDataType(document.Root!, document.Root!);
        Walk(document.Root!, document.Root!, rootType, rootType, offenders, ref checkedPaths);

        checkedPaths.Should().BeGreaterThan(minimumBindings - 1,
            "la sonda debe estar viendo los enlaces reales de la vista");
        offenders.Should().BeEmpty(
            "con los enlaces compilados desactivados, un camino que no existe no falla: deja el control mudo " +
            "(campos vacíos y botones que no hacen nada). Sin resolver en {0}: {1}",
            relativePath, string.Join(" | ", offenders));
    }

    [Fact]
    public void TheDownloadDialog_ShouldHostTheSharedManagerViewInsteadOfItsOwnList()
    {
        // El diálogo tenía su propia lista, con enlaces a miembros que no existen (SizeText, IsInstalled,
        // DownloadCommand, DeleteCommand): abría una lista sin tallas, sin estado y con botones mudos. La lista
        // vive ahora en AiModelManagerView, que es la misma pieza que hospeda la pestaña de ajustes.
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), DownloadDialogXaml);
        string xaml = File.ReadAllText(path);

        xaml.Should().Contain("AiModelManagerView",
            "el asistente de descarga debe envolver el gestor compartido, no duplicar la lista de modelos");
        xaml.Should().NotContain("<ItemsControl",
            "la lista de modelos vive en AiModelManagerView: dos listas son dos sitios donde un enlace puede dejar de resolver");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. Toda clave de traducción usada por la UI existe
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryResourceKeyReferencedByTheXaml_ShouldExistInSomeDictionary()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var known = new HashSet<string>(StringComparer.Ordinal);
        var knownIgnoringCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string resx in Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories)
                     .Where(p => !IsBuildArtifact(p)))
        {
            foreach (string key in Regex.Matches(File.ReadAllText(resx), "<data name=\"([^\"]+)\"")
                         .Select(m => m.Groups[1].Value))
            {
                known.Add(key);
                knownIgnoringCase.TryAdd(key, key);
            }
        }

        known.Count.Should().BeGreaterThan(500, "el inventario debe estar leyendo los diccionarios reales del repositorio");

        var missing = new List<string>();
        var wrongCase = new List<string>();
        int references = 0;

        foreach (string xaml in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
                     .Where(p => !IsBuildArtifact(p)))
        {
            string relative = Path.GetRelativePath(root, xaml).Replace('\\', '/');

            foreach (Match match in Regex.Matches(File.ReadAllText(xaml), @"Path=\[([A-Za-z0-9_]+)\]"))
            {
                string key = match.Groups[1].Value;
                references++;

                if (known.Contains(key))
                {
                    continue;
                }

                // Una variante que sólo cambia de mayúsculas es un duplicado para el compilador de recursos
                // (MSB3568) y no resuelve en tiempo de ejecución: se nombra el nombre correcto.
                if (knownIgnoringCase.TryGetValue(key, out string? correct))
                {
                    wrongCase.Add($"{relative}: '{key}' (existe '{correct}')");
                }
                else
                {
                    missing.Add($"{relative}: '{key}'");
                }
            }
        }

        references.Should().BeGreaterThan(200, "el lint debe estar viendo los enlaces a recursos reales");

        missing.Should().BeEmpty(
            "una clave inexistente no es un fallo de enlace: el indexador devuelve cadena vacía, FallbackValue no " +
            "se aplica y la etiqueta queda EN BLANCO sin cambiar de idioma. Sin traducir: " + string.Join(" | ", missing));

        wrongCase.Should().BeEmpty(
            "una clave que sólo difiere en mayúsculas es un duplicado para el compilador de recursos y no resuelve " +
            "en tiempo de ejecución. " + string.Join(" | ", wrongCase));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Ayudas
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Texto visible de una cabecera de pestaña (la cabecera mezcla icono vectorial y etiqueta).</summary>
    private static string VisibleHeaderText(TabItem tab) => tab.Header switch
    {
        string text => text,
        Control control => string.Join(
            " ",
            control.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))),
        _ => string.Empty
    };

    /// <summary>¿El texto contiene un pictograma que depende de las fuentes del sistema?</summary>
    private static bool IsPictograph(char c) =>
        (c >= 0x1F000 && c <= 0x1FAFF) ||
        (c >= 0x2600 && c <= 0x27BF) ||
        (c >= 0x2190 && c <= 0x21FF) ||
        (c >= 0x2B00 && c <= 0x2BFF) ||
        (c >= 0x25A0 && c <= 0x25FF) ||
        (c >= 0x23E9 && c <= 0x23FA);

    private static bool IsBuildArtifact(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Recorre el árbol llevando el tipo de datos vigente. Un <c>DataTemplate</c> cambia el contexto de enlace:
    /// sus caminos se validan contra el tipo que declara, no contra el de la vista.
    /// </summary>
    private static void Walk(XElement element, XElement rootElement, Type? root, Type? dataType, List<string> offenders, ref int checkedPaths)
    {
        Type? current = dataType;

        if (element.Name.LocalName == "DataTemplate" && ResolveDataType(element, rootElement) is { } declared)
        {
            current = declared;
        }

        foreach (XAttribute attribute in element.Attributes())
        {
            string name = attribute.Name.LocalName;

            // Enlaces de lista: se resuelven contra el elemento de la colección, no contra el DataContext.
            if (name is "SelectedValueBinding" or "DisplayMemberBinding")
            {
                continue;
            }

            string value = attribute.Value;

            Match binding = Regex.Match(value, @"\{Binding\s+([^,}]*)");

            if (!binding.Success)
            {
                continue;
            }

            string path = binding.Groups[1].Value.Trim().TrimStart('!');

            if (path.Length == 0 || path.Contains('(') || path.Contains('='))
            {
                continue; // {Binding}, conversores, Source={...}
            }

            // Los comandos de una plantilla se enlazan contra el view model de la vista («$parent[...].DataContext.X»).
            // Se validan contra el tipo raíz, no contra el del elemento de la lista.
            Match parentMember = Regex.Match(path, @"\$parent\[[^\]]+\]\.(?:DataContext\.)?([A-Za-z0-9_]+)");

            if (parentMember.Success)
            {
                checkedPaths++;
                string member = parentMember.Groups[1].Value;

                if (root == null || root.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) == null)
                {
                    offenders.Add(
                        $"{element.Name.LocalName}.{name}: '{path}' no existe en {(root == null ? "<sin x:DataType>" : root.Name)}");
                }

                continue;
            }

            if (path.Contains('$'))
            {
                continue; // $self y otros anclajes sin miembro que validar
            }

            string first = path.Split('.')[0];
            checkedPaths++;

            if (current == null)
            {
                offenders.Add($"{element.Name.LocalName}.{name}: '{path}' (la vista no declara x:DataType)");
                continue;
            }

            if (current.GetProperty(first, BindingFlags.Public | BindingFlags.Instance) == null &&
                current.GetProperty(first, BindingFlags.Public | BindingFlags.Static) == null)
            {
                offenders.Add($"{element.Name.LocalName}.{name}: '{path}' no existe en {current.Name}");
            }
        }

        foreach (XElement child in element.Elements())
        {
            Walk(child, rootElement, root, current, offenders, ref checkedPaths);
        }
    }

    /// <summary>Resuelve <c>x:DataType="vm:WorkflowSettingsViewModel"</c> a un <see cref="Type"/> real.</summary>
    private static Type? ResolveDataType(XElement element, XElement documentRoot)
    {
        string? raw = element.Attribute(XName.Get("DataType", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
            ?? element.Attributes().FirstOrDefault(a => a.Name.LocalName == "DataType")?.Value
            ?? documentRoot.Attribute(XName.Get("DataType", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;

        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains(':'))
        {
            return null;
        }

        string prefix = raw[..raw.IndexOf(':')];
        string typeName = raw[(raw.IndexOf(':') + 1)..];

        string? ns = documentRoot.Attributes()
            .FirstOrDefault(a => a.Name.Namespace == XNamespace.Xmlns && a.Name.LocalName == prefix)
            ?.Value;

        if (ns == null || !ns.StartsWith("using:", StringComparison.Ordinal))
        {
            return null;
        }

        string fullName = $"{ns["using:".Length..]}.{typeName}";

        return typeof(LocalizationManager).Assembly.GetType(fullName)
               ?? typeof(FileFlow.App.App).Assembly.GetType(fullName)
               ?? AppDomain.CurrentDomain.GetAssemblies()
                   .Select(a => a.GetType(fullName))
                   .FirstOrDefault(t => t != null);
    }
}
