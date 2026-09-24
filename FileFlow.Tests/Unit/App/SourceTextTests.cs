using System;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El analizador con el que los lints leen el <b>código</b> en lugar del archivo.
///
/// <para>Se prueba porque seis guardias dependen de él y porque equivocarse aquí no se nota: un texto alterado
/// hace que un lint no encuentre lo que busca (o encuentre lo que solo aparece en un comentario) y su verde pasa
/// a significar otra cosa. Tenía justamente ese agujero —se comía el carácter siguiente a cada literal, así que
/// una llamada se leía sin su paréntesis de cierre y una lista `"a", "b"` llegaba como `"a" "b"`—.</para>
/// </summary>
public class SourceTextTests
{
    [Fact]
    public void ALiteral_ShouldKeepTheCharacterThatFollowsIt()
    {
        string code = SourceText.WithoutComments(
            "var x = Pseudo(control, \":pointerover\");\nvar list = [\"uno\", \"dos\"];");

        code.Should().Contain("Pseudo(control, \":pointerover\");",
            "una llamada tiene que leerse entera: el analizador consumía un carácter de más tras cada literal");
        code.Should().Contain("[\"uno\", \"dos\"]",
            "los literales contiguos conservan su coma y sus corchetes");
    }

    [Fact]
    public void Comments_ShouldBeRemoved_WithoutTouchingTheLiteralsThatContainThem()
    {
        string code = SourceText.WithoutComments(
            "// un lint que buscase 'Foo()' no debe conformarse con esto\n" +
            "var url = \"https://ejemplo/x\"; /* y este bloque tampoco */\n" +
            "Foo();");

        code.Should().NotContain("no debe conformarse");
        code.Should().NotContain("y este bloque tampoco");
        code.Should().Contain("\"https://ejemplo/x\"", "dentro de un literal, '//' no abre un comentario");
        code.Should().Contain("Foo();");
    }

    /// <summary>
    /// El comentario se lleva su texto, <b>no su línea</b>. Se prueba porque el fallo contrario es invisible: la
    /// versión que se comía el terminador dejaba de ver que un atributo está en su propia línea —lo fundía con el
    /// método de abajo— y con él se vaciaba el índice de pruebas en <b>158 de los 227</b> ficheros del suite, sin
    /// que ninguna guardia lo notara hasta que una auditoría de puertos pasó en verde sin mirar nada.
    /// </summary>
    [Fact]
    public void AComment_ShouldNotSwallowTheLineBreakThatEndsItsLine()
    {
        const string source =
            "    [InlineData(\"Out\", 1)]   // nota\r\n" +
            "    public void UnCaso()   // otra nota\r\n" +
            "    {\r\n" +
            "    }\r\n";

        string code = SourceText.WithoutComments(source);

        code.Count(c => c == '\n').Should().Be(source.Count(c => c == '\n'),
            "quitar comentarios no quita líneas: lo que sale tiene que seguir siendo el mismo código");

        string[] lines = [.. code.Split('\n').Select(line => line.Trim())];
        int method = Array.IndexOf(lines, "public void UnCaso()");

        method.Should().BeGreaterThan(0, "el método tiene que seguir en una línea propia");
        lines[method - 1].Should().Be("[InlineData(\"Out\", 1)]",
            "un atributo con un comentario al lado sigue siendo la línea anterior del método: es lo que permite " +
            "atribuir un caso de prueba a su método");
    }

    /// <summary>
    /// Y un bloque conserva sus saltos y <b>no</b> se come el carácter siguiente. Las dos mitades importaron: los
    /// saltos, porque borraban líneas enteras de código; el carácter, porque `i += 2` dejaba el índice pasado el
    /// `*/` y el `i++` del bucle exterior se llevaba por delante el carácter de después.
    /// </summary>
    [Fact]
    public void ABlockComment_ShouldKeepItsLineBreaksAndTheCharacterThatFollowsIt()
    {
        const string source =
            "[Fact]\n" +
            "/* una nota\n" +
            "   de dos líneas */\n" +
            "public void UnCaso() { }\n";

        string code = SourceText.WithoutComments(source);

        code.Count(c => c == '\n').Should().Be(source.Count(c => c == '\n'),
            "las dos líneas del bloque tienen que contar como dos líneas");
        code.Should().Contain("public void UnCaso() { }",
            "el carácter que sigue al cierre del bloque se conserva");
    }
}
