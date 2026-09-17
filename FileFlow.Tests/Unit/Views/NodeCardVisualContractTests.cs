using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FileFlow.App.ViewModels;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Contrato visual de la tarjeta de nodo y de los cables del lienzo.
///
/// Avalonia resuelve los bindings por reflexión en este proyecto y una ruta inexistente NO rompe la
/// compilación: el elemento se queda vacío y el fallo sólo aparece si alguien mira la pantalla. Este test
/// cierra esa clase de error leyendo el XAML y comprobando cada ruta contra los view models reales.
/// </summary>
[Collection("ThemeTokens")]
public class NodeCardVisualContractTests
{
    private const string CardRelativePath = "FileFlow.App/Views/Components/NodeCardView.axaml";
    private const string EditorRelativePath = "FileFlow.App/Views/EditorView.axaml";

    /// <summary>Rutas simples o con un nivel de anidamiento: el caso habitual en este XAML.</summary>
    private static readonly Regex BindingPathRegex = new(
        @"\{Binding\s+!?([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*[,}]",
        RegexOptions.Compiled);

    private static readonly Regex DataTemplateContextRegex = new(
        @"<DataTemplate\b[^>]*DataType=""vm:([A-Za-z0-9_]+)""",
        RegexOptions.Compiled);

    private static readonly Regex ClosedDataTemplateRegex = new(@"</DataTemplate>", RegexOptions.Compiled);

    /// <summary>View models del proyecto a los que puede apuntar un DataType declarado en la tarjeta.</summary>
    private static readonly Dictionary<string, Type> KnownDataContexts = new(StringComparer.Ordinal)
    {
        [nameof(NodeViewModel)] = typeof(NodeViewModel),
        [nameof(PortViewModel)] = typeof(PortViewModel),
        [nameof(NodeActionViewModel)] = typeof(NodeActionViewModel)
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // Bindings
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryCardBinding_ShouldResolveAgainstTheViewModelOfItsTemplate()
    {
        // Cada binding se comprueba contra el DataType de la plantilla en la que vive, no contra un
        // conjunto de candidatos: así se detecta también el caso sutil de una ruta que existe en OTRO view
        // model (p. ej. 'Description' del nodo dentro de la plantilla de acciones del nodo).
        string card = ReadRepositoryFile(CardRelativePath);
        var unresolved = new List<string>();
        var unknownContexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (line, path, contextName) in ExtractBindingsWithContext(card, unknownContexts))
        {
            if (KnownDataContexts.TryGetValue(contextName, out var context) && ResolvesPath(context, path))
            {
                continue;
            }

            unresolved.Add($"{Path.GetFileName(CardRelativePath)}:{line} '{path}' sobre {contextName}");
        }

        unknownContexts.Should().BeEmpty(
            "toda plantilla de la tarjeta debe declarar un DataType conocido para poder validar sus bindings. " +
            "Desconocidos: " + string.Join(", ", unknownContexts));

        unresolved.Should().BeEmpty(
            "una ruta de binding que no existe en el view model deja el elemento vacío en tiempo de ejecución " +
            "sin fallar la compilación (los bindings son por reflexión). Rutas muertas: " + string.Join(" | ", unresolved));
    }

    [Fact]
    public void TheCard_ShouldBindTelemetryToRawNumbers_NotToFormattedStrings()
    {
        // El pie muestra el mismo dato en cualquier idioma: se formatea con convertidores en la vista.
        string card = ReadRepositoryFile(CardRelativePath);

        card.Should().Contain("CurrentStats.ProcessedCount");
        card.Should().Contain("CurrentStats.RollingAvgAllocatedBytes");
        card.Should().Contain("DurationMsToTextConverter");
        card.Should().Contain("BytesToTextConverter");
        card.Should().NotContain("LatencyText", "la telemetría se formatea en la vista, no en el view model");
        card.Should().NotContain("RollingRamText");
    }

    [Fact]
    public void TheCardFooter_ShouldHideMetricsUntilTheNodeHasRun()
    {
        string card = ReadRepositoryFile(CardRelativePath);

        card.Should().Contain("IsVisible=\"{Binding HasTelemetry}\"");
        card.Should().Contain("IsVisible=\"{Binding HasRamTelemetry}\"");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sockets
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BothPortDirections_ShouldShareOneSocketTemplate()
    {
        string card = ReadRepositoryFile(CardRelativePath);

        Occurrences(card, "x:Key=\"PortSocketTemplate\"").Should().Be(1,
            "el socket se define una sola vez para que entrada y salida no puedan divergir");
        Occurrences(card, "ContentTemplate=\"{StaticResource PortSocketTemplate}\"").Should().Be(2,
            "tanto los puertos de entrada como los de salida deben usar la plantilla compartida");
        Occurrences(card, "Classes=\"socket\"").Should().Be(1,
            "el socket no debe dibujarse con estilos sueltos fuera de su plantilla");
        Occurrences(card, "Classes=\"socketTriangle\"").Should().Be(1);
    }

    [Fact]
    public void TheSocketTemplate_ShouldFeedEveryVisualStateFromTheViewModel()
    {
        string card = ReadRepositoryFile(CardRelativePath);

        // Formas, familias de tipo y estados de arrastre: si falta una de estas clases, esa categoría de
        // puerto se queda sin forma, sin color o sin resaltado al arrastrar.
        foreach (string state in new[]
                 {
                     "shapeCircle", "shapeSquare", "shapeDiamond",
                     "typeFiles", "typeText", "typeBoolean", "typeNumber", "typeBinary", "typeCollection", "typeAny",
                     "connected", "dragSource", "compatible", "compatibleWarning", "incompatible"
                 })
        {
            card.Should().Contain($"Classes.{state}=", $"la plantilla del socket debe reflejar el estado '{state}'");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Cables
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheConnectionTemplate_ShouldDefineStandardConnectionWire()
    {
        string editor = ReadRepositoryFile(EditorRelativePath);
        string template = ExtractConnectionTemplate(editor);

        template.Should().Contain("<nodifyConn:Connection ", "el lienzo debe declarar la conexión Nodify");
        template.Should().Contain("Source=\"{Binding Source.Anchor}\"", "el cable debe conectarse al ancla de origen");
        template.Should().Contain("Target=\"{Binding Target.Anchor}\"", "el cable debe conectarse al ancla de destino");
        template.Should().NotContain("Classes=\"energy\"", "las animaciones de guiones superpuestas fueron eliminadas");
    }

    [Fact]
    public void TheContextMenu_ShouldHangFromTheInteractiveWire()
    {
        string template = ExtractConnectionTemplate(ReadRepositoryFile(EditorRelativePath));

        int interactiveWire = template.IndexOf("<nodifyConn:Connection ", StringComparison.Ordinal);
        int contextMenu = template.IndexOf("<nodifyConn:Connection.ContextMenu>", StringComparison.Ordinal);

        interactiveWire.Should().BeGreaterThanOrEqualTo(0, "el cable base debe existir");
        contextMenu.Should().BeGreaterThan(interactiveWire,
            "el menú contextual debe colgar del cable de conexión");
    }

    [Fact]
    public void TheConnectionStyles_ShouldExistForEveryWireType()
    {
        string ports = ReadRepositoryFile("FileFlow.App/Styles/Ports.axaml");

        foreach (string type in new[] { "wireFiles", "wireText", "wireBoolean", "wireNumber", "wireBinary", "wireCollection", "wireAny" })
        {
            ports.Should().Contain($"nodifyConn|Connection.{type}",
                $"el cable '{type}' debe usar el color de su familia de tipo");
        }

        // Los sockets booleanos y numéricos se dibujan con formas distintas (Path y Border rotado): cada uno
        // necesita sus propios estados de arrastre, o esa familia de puertos se queda sin resaltado.
        ports.Should().Contain("Path.socketTriangle.compatible");
        ports.Should().Contain("Path.socketTriangle.compatibleWarning");
        ports.Should().Contain("Border.socket.shapeDiamond.compatible");
        ports.Should().Contain("Border.socket.shapeDiamond.compatibleWarning");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Textos: la tarjeta y los sockets son UI, así que su texto debe estar traducido
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CardAndPortTexts_ShouldExistInBothLanguages()
    {
        // Claves cuyo valor es idéntico en los dos idiomas por ser una unidad, un símbolo o un término que
        // se escribe igual; se documentan una a una para que el resto siga exigiendo traducción.
        var identicalByDesign = new HashSet<string>(StringComparer.Ordinal)
        {
            "Node_BottleneckPercent", // "{0}%" es un formato, no una frase.
            "NodeStatus_Faulted"      // "Error" se escribe igual en español y en inglés.
        };

        var english = ReadResourceFile("FileFlow.App/Resources/Strings.resx");
        var spanish = ReadResourceFile("FileFlow.App/Resources/Strings.es.resx");

        var keys = new List<string>();

        foreach (string source in new[] { "FileFlow.App/ViewModels/PortViewModel.cs", "FileFlow.App/ViewModels/NodeViewModel.cs" })
        {
            keys.AddRange(ExtractLocalizationKeys(ReadRepositoryFile(source)));
        }

        keys = keys.Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        keys.Should().HaveCountGreaterThan(10, "la tarjeta y los sockets usan textos localizados");

        var missingEnglish = keys.Where(k => !english.ContainsKey(k)).ToList();
        var missingSpanish = keys.Where(k => !spanish.ContainsKey(k)).ToList();
        var untranslated = keys
            .Where(k => english.ContainsKey(k) && spanish.ContainsKey(k))
            .Where(k => !identicalByDesign.Contains(k) && english[k] == spanish[k])
            .ToList();

        missingEnglish.Should().BeEmpty("toda clave usada por el view model debe existir en Strings.resx, no sólo en el fallback del código: " + string.Join(", ", missingEnglish));
        missingSpanish.Should().BeEmpty("toda clave usada por el view model debe existir en Strings.es.resx: " + string.Join(", ", missingSpanish));
        untranslated.Should().BeEmpty("una clave con el mismo texto en inglés y español suele ser una traducción olvidada: " + string.Join(", ", untranslated));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static readonly Regex LocalizationKeyRegex = new(
        @"Get(?:Formatted)?String\(""([A-Za-z0-9_]+)""",
        RegexOptions.Compiled);

    private static IEnumerable<string> ExtractLocalizationKeys(string source)
        => LocalizationKeyRegex.Matches(source).Select(m => m.Groups[1].Value);

    private static Dictionary<string, string> ReadResourceFile(string relativePath)
    {
        string text = ReadRepositoryFile(relativePath);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in Regex.Matches(text, @"<data name=""([^""]+)""[^>]*>\s*<value>(.*?)</value>", RegexOptions.Singleline))
        {
            entries[match.Groups[1].Value] = match.Groups[2].Value;
        }

        entries.Should().NotBeEmpty($"'{relativePath}' debe contener recursos");

        return entries;
    }

    private static string ReadRepositoryFile(string relativePath)
        => File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    private static int Occurrences(string text, string needle)
        => text.Split(needle, StringSplitOptions.None).Length - 1;

    /// <summary>Recorta el bloque de la plantilla de cables para no confundirlo con la de conexión en curso.</summary>
    private static string ExtractConnectionTemplate(string editor)
    {
        int start = editor.IndexOf("<nodify:NodifyEditor.ConnectionTemplate>", StringComparison.Ordinal);
        int end = editor.IndexOf("</nodify:NodifyEditor.ConnectionTemplate>", StringComparison.Ordinal);

        start.Should().BeGreaterThanOrEqualTo(0, "el lienzo debe declarar la plantilla de cables");
        end.Should().BeGreaterThan(start);

        return editor[start..end];
    }

    /// <summary>
    /// Recorre el XAML manteniendo la pila de plantillas abiertas: los bindings se asocian al DataType de
    /// la plantilla más interna en la que aparecen, y al DataContext del control (el nodo) si están fuera
    /// de cualquier plantilla.
    /// </summary>
    private static IEnumerable<(int Line, string Path, string Context)> ExtractBindingsWithContext(
        string xaml,
        HashSet<string> unknownContexts)
    {
        var stack = new Stack<string>();
        int line = 1;

        foreach (string rawLine in xaml.Replace("\r\n", "\n").Split('\n'))
        {
            // Las etiquetas de apertura/cierre del DataTemplate van en su propia línea en estos ficheros.
            foreach (Match opened in DataTemplateContextRegex.Matches(rawLine))
            {
                string context = opened.Groups[1].Value;
                stack.Push(context);

                if (!KnownDataContexts.ContainsKey(context))
                {
                    unknownContexts.Add(context);
                }
            }

            if (ClosedDataTemplateRegex.IsMatch(rawLine))
            {
                // Una sola línea puede cerrar más de una plantilla.
                foreach (Match _ in ClosedDataTemplateRegex.Matches(rawLine))
                {
                    if (stack.Count > 0) stack.Pop();
                }
            }

            string contextForLine = stack.Count > 0 ? stack.Peek() : nameof(NodeViewModel);

            foreach (Match match in BindingPathRegex.Matches(rawLine))
            {
                yield return (line, match.Groups[1].Value, contextForLine);
            }

            line++;
        }
    }

    /// <summary>
    /// ¿Existe la ruta en el tipo? Recorre cada segmento por reflexión, de modo que una ruta anidada como
    /// <c>CurrentStats.ProcessedCount</c> sólo se acepta si el primer miembro existe y el segundo existe en
    /// el tipo que éste devuelve.
    /// </summary>
    private static bool ResolvesPath(Type rootType, string path)
    {
        Type? current = rootType;

        foreach (string segment in path.Split('.'))
        {
            if (current is null)
            {
                return false;
            }

            var property = current.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (property is null)
            {
                return false;
            }

            current = property.PropertyType;
        }

        return true;
    }
}
