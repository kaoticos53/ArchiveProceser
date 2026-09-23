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
}
