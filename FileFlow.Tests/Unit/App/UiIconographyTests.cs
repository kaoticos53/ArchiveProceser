using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.App.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Material.Icons;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias de la iconografía vectorial.
///
/// Los emojis dependen de las fuentes instaladas en cada sistema: en Windows se ven a color, pero en
/// Linux sin Noto Color Emoji aparecen como cuadraditos ("tofu") o monocromos, y en macOS con otro
/// diseño. La aplicación es multiplataforma por definición, así que la UI no debe depender de ellos.
/// </summary>
[Collection("ThemeTokens")]
public class UiIconographyTests
{

    /// <summary>
    /// Glifos admitidos que NO son iconografía: máscaras de contraseña y separadores tipográficos.
    /// Se documentan uno a uno para que el resto de emojis siga prohibido.
    /// </summary>
    private static readonly HashSet<string> AllowedGlyphs = new(StringComparer.Ordinal)
    {
        "\u25CF", // ● Máscara de campo de contraseña (PasswordChar), no es un icono.
    };

    private static readonly Regex KindLiteralRegex = new(
        @"Kind=""([A-Za-z0-9_]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// ¿Es el punto de código un emoji o un pictograma que depende de fuentes del sistema?
    /// Se compara por punto de código (no por regex) porque .NET no admite rangos por encima del BMP
    /// en su motor de expresiones regulares.
    /// </summary>
    private static bool IsEmojiCodePoint(int codePoint) =>
        (codePoint >= 0x1F000 && codePoint <= 0x1FAFF) ||
        (codePoint >= 0x2600 && codePoint <= 0x27BF) ||
        (codePoint >= 0x2190 && codePoint <= 0x21FF) ||
        (codePoint >= 0x2B00 && codePoint <= 0x2BFF) ||
        (codePoint >= 0x25A0 && codePoint <= 0x25FF) ||
        (codePoint >= 0x23E9 && codePoint <= 0x23FA) ||
        IsEmojiPresentationPictograph(codePoint);

    /// <summary>
    /// Pictogramas con presentación emoji que viven FUERA de los bloques principales (Letterlike Symbols,
    /// símbolos varios y compatibilidad CJK). Se enumeran uno a uno en lugar de vetar el bloque entero:
    /// en esos rangos conviven signos tipográficos legítimos (©, ®, grados, marcas de párrafo) que sí
    /// pueden aparecer en textos de interfaz y en documentación.
    /// </summary>
    private static bool IsEmojiPresentationPictograph(int codePoint) =>
        codePoint is 0x2139  // ℹ información
            or 0x203C        // ‼ exclamación doble
            or 0x2049        // ⁉ interrogación exclamativa
            or 0x24C2        // Ⓜ círculo con M
            or 0x2934        // ⤴ flecha curvada
            or 0x2935        // ⤵
            or 0x3030        // 〰 raya ondulada
            or 0x303D        // 〽 marca alternante
            or 0x3297        // ㊗ «felicitación» japonesa
            or 0x3299;       // ㊙ «secreto» japonés

    /// <summary>Recorre el texto por punto de código y devuelve los pictogramas con su línea y columna.</summary>
    private static IEnumerable<(int Line, int Column, string Glyph)> FindEmojiPictographs(string path)
    {
        int lineNumber = 0;

        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;

            for (int i = 0; i < line.Length; i++)
            {
                int codePoint;

                if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(line[i], line[i + 1]);
                    i++;
                }
                else
                {
                    codePoint = line[i];
                }

                if (IsEmojiCodePoint(codePoint))
                {
                    yield return (lineNumber, i, char.ConvertFromUtf32(codePoint));
                }
            }
        }
    }

    /// <summary>
    /// Tramos comentados de cada línea (<c>&lt;!-- ... --&gt;</c>), incluyendo los que vienen abiertos de una
    /// línea anterior. Se calculan por posición para que un comentario al final de una línea no exima a los
    /// pictogramas reales que aparezcan antes en esa misma línea.
    /// </summary>
    private static List<(int Start, int End)>[] CommentedSpans(string path)
    {
        string[] lines = File.ReadAllLines(path);
        var spans = new List<(int Start, int End)>[lines.Length];
        bool inside = false;
        int blockStart = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            spans[i] = [];
            string line = lines[i];

            if (inside)
            {
                blockStart = 0;
            }

            int index = 0;

            while (true)
            {
                if (inside)
                {
                    int close = line.IndexOf("-->", index, StringComparison.Ordinal);

                    if (close < 0)
                    {
                        spans[i].Add((blockStart, line.Length));
                        break;
                    }

                    spans[i].Add((blockStart, close + 3));
                    inside = false;
                    index = close + 3;
                    continue;
                }

                int open = line.IndexOf("<!--", index, StringComparison.Ordinal);

                if (open < 0)
                {
                    break;
                }

                inside = true;
                blockStart = open;
                index = open + 4;
            }
        }

        return spans;
    }

    private static bool IsCommented(List<(int Start, int End)>[] spans, int line, int column)
        => spans[line - 1].Any(span => column >= span.Start && column < span.End);

    private static IEnumerable<string> UiXamlFiles()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        return Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    [Fact]
    public void UiXaml_ShouldNotContainEmojiIconography()
    {
        var offenders = new List<string>();

        foreach (string file in UiXamlFiles())
        {
            // La regla es sobre la UI, no sobre la documentación: los comentarios XML pueden explicarse con
            // flechas o tablas (hay glifos que caen dentro del rango de pictogramas) sin que eso llegue nunca
            // a la pantalla. Se descartan por posición, no por línea completa.
            var commentSpans = CommentedSpans(file);

            foreach (var (lineNumber, column, glyph) in FindEmojiPictographs(file))
            {
                if (AllowedGlyphs.Contains(glyph) || IsCommented(commentSpans, lineNumber, column))
                {
                    continue;
                }

                offenders.Add($"{Path.GetFileName(file)}:{lineNumber} '{glyph}'");
            }
        }

        offenders.Should().BeEmpty(
            "la UI no debe usar emojis como iconos (dependen de las fuentes del sistema y se ven distintos o como " +
            "cuadraditos en Linux/macOS). Usa un MaterialIcon vectorial o añade el glifo a AllowedGlyphs con su motivo. " +
            "Restos: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void EveryKindLiteralInXaml_ShouldBeADefinedMaterialIconKind()
    {
        var invalid = new List<string>();
        int total = 0;

        foreach (string file in UiXamlFiles())
        {
            string text = File.ReadAllText(file);

            foreach (Match match in KindLiteralRegex.Matches(text))
            {
                string name = match.Groups[1].Value;

                // En algunos sitios la clave del icono llega por binding: {Binding Icon}.
                if (name.StartsWith("Binding", StringComparison.Ordinal))
                {
                    continue;
                }

                total++;
                if (!Enum.TryParse<MaterialIconKind>(name, ignoreCase: false, out _))
                {
                    invalid.Add($"{Path.GetFileName(file)}: Kind=\"{name}\"");
                }
            }
        }

        total.Should().BeGreaterThan(50, "la UI debe consumir la iconografía vectorial");
        invalid.Should().BeEmpty(
            "todo Kind literal debe existir en el enum MaterialIconKind (una errata falla en tiempo de ejecución o de compilación XAML). " +
            "Inválidos: " + string.Join(" | ", invalid));
    }

    [Fact]
    public void NodeIconResolver_ShouldCoverEveryNodeTypeWithAVectorIcon()
    {
        // Los tipos de nodo registrados por los plugins deben tener icono propio (tabla exacta o heurística),
        // no el icono de reserva: son los iconos del lienzo, del toolbox y del panel de métricas.
        string[] knownTypes =
        [
            "FolderSourceNode", "DestinationSinkNode", "SmartUnpackNode", "ArchiveCompressorNode",
            "ImageOptimizerNode", "MediaTranscoderNode", "LocalOcrNode", "FaceDetectorNode",
            "WhisperTranscriptionNode", "DocumentProcessorNode", "PdfMergeNode", "NetworkDownloadNode",
            "NetworkUploadNode", "ExcelDataSourceNode", "CsvDataSourceNode", "SqliteExportNode",
            "HashCalculatorNode", "SwitchCaseNode", "OperationReportNode", "LogOutputNode",
            "WebhookNotificationNode", "CliExecutionNode", "CustomScriptNode", "AdvancedRenamerNode"
        ];

        var missing = knownTypes
            .Where(t => NodeIconResolver.GetIconForNodeType(t) == NodeIconResolver.FallbackNodeIcon)
            .ToList();

        missing.Should().BeEmpty("todo tipo de nodo conocido debe resolverse a un icono vectorial propio. Sin icono: " + string.Join(", ", missing));
    }

    [Fact]
    public void NodeIconResolver_ShouldTranslateLegacyEmojiValues()
    {
        // El contrato del SDK (NodeActionDescriptor.Icon) y los flujos guardados antes de la migración
        // siguen transportando emojis: deben traducirse, nunca acabar en el icono de reserva.
        foreach (var (legacy, expected) in new (string, MaterialIconKind)[]
                 {
                     ("⚙️", MaterialIconKind.Cog),
                     ("📁", MaterialIconKind.Folder),
                     ("📄", MaterialIconKind.FileDocument),
                     ("✅", MaterialIconKind.CheckCircle),
                     ("❌", MaterialIconKind.CloseCircle),
                     ("📦", MaterialIconKind.ZipBox),
                     ("🔍", MaterialIconKind.Magnify),
                     ("🧩", MaterialIconKind.Puzzle)
                 })
        {
            NodeIconResolver.GetIconForAction(legacy).Should().Be(expected, $"'{legacy}' debe traducirse al icono {expected}");
        }

        // Valores ya migrados (nombre del enum) también son válidos.
        NodeIconResolver.GetIconForAction(nameof(MaterialIconKind.Play)).Should().Be(MaterialIconKind.Play);

        // Texto desconocido: icono de reserva, nunca una excepción.
        NodeIconResolver.GetIconForAction("texto sin icono").Should().Be(NodeIconResolver.FallbackActionIcon);
        NodeIconResolver.GetIconForAction(null).Should().Be(NodeIconResolver.FallbackActionIcon);
    }

    [Fact]
    public void IconStyles_ShouldBeRegisteredInAppAxaml()
    {
        string app = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/App.axaml");
        string text = File.ReadAllText(app);

        text.Should().Contain("materialIcons:MaterialIconStyles",
            "sin registrar los estilos del control MaterialIcon, los iconos no se dibujan");
        text.Should().Contain("xmlns:materialIcons=\"clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia\"");
    }
}
