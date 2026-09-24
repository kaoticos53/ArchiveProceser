using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El índice de casos del suite, del que dependen las guardias que citan pruebas como evidencia.
///
/// <para>Se prueba porque sus dos fallos posibles son <b>silenciosos</b> y ya han ocurrido los dos: leer el árbol
/// a medias deja a una guardia pasando en verde sin haber mirado nada, y tardar un minuto y medio por fichero la
/// convierte en una guardia que nadie ejecuta. Aquí se fijan las dos cosas: lo que el analizador ve en fuentes
/// sintéticas —que es donde se puede afirmar sin depender del árbol—, que <b>ningún</b> fichero con casos se
/// quede sin casos, y que leer el suite entero siga costando milisegundos.</para>
/// </summary>
public class TestSuiteIndexTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // El analizador, sobre fuentes sintéticas
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ACase_ShouldBeReadWithItsAttributesAndItsBody()
    {
        var blocks = TestSuiteIndex.BlocksOf("Sintetico.cs", """
            public class ConCasos
            {
                [Fact]
                public void UnCaso()
                {
                    Assert.True(true);
                }

                public void NoEsCaso()
                {
                }
            }
            """);

        blocks.Should().ContainSingle().Which.Name.Should().Be("UnCaso",
            "un método público sin atributo de prueba no es un caso");
        blocks[0].Source.Should().Contain("[Fact]").And.Contain("Assert.True(true)",
            "el bloque es el caso entero: sus atributos, su firma y su cuerpo");
    }

    /// <summary>
    /// Un atributo con corchetes dentro (<c>[InlineData(new[] { … })]</c>) sigue siendo un atributo. La expresión
    /// regular que este analizador sustituyó era <b>ciega</b> a ellos —no podía atravesar un <c>]</c>—, así que
    /// tres teorías reales del suite no existían para ella, y una de ellas prueba justamente la sesión headless.
    /// </summary>
    [Fact]
    public void ATheoryWhoseDataCarriesBrackets_ShouldStillBeACase()
    {
        var blocks = TestSuiteIndex.BlocksOf("Sintetico.cs", """
            public class ConCasos
            {
                [Theory]
                [InlineData(new[] { 1, 2 }, "dos")]
                [InlineData(new string[0], "ninguno")]
                public void UnCasoTeorico(int[] valores, string nombre)
                {
                    Assert.NotEmpty(nombre);
                }
            }
            """);

        blocks.Should().ContainSingle().Which.Name.Should().Be("UnCasoTeorico");
        blocks[0].Source.Should().Contain("new[] { 1, 2 }").And.Contain("new string[0]",
            "los datos de la teoría forman parte del caso");
    }

    [Fact]
    public void ABlankLineBetweenTheAttributeAndTheMethod_ShouldNotBreakTheCase()
    {
        var blocks = TestSuiteIndex.BlocksOf("Sintetico.cs", """
            public class ConCasos
            {
                [Fact]

                public void UnCaso()
                {
                    Assert.True(true);
                }
            }
            """);

        blocks.Should().ContainSingle().Which.Name.Should().Be("UnCaso",
            "en C# una línea en blanco no separa un atributo de la declaración que describe");
    }

    /// <summary>
    /// La tabla de datos entra en el bloque <b>del caso que la cita</b> y en ningún otro: es lo que permite que un
    /// caso con parámetros cite el nodo y el puerto que afirma cubrir, que es como el censo de puertos escribe su
    /// evidencia. Y el corte del bloque es el final del cuerpo, no la declaración siguiente: sin eso, un caso
    /// heredaría la tabla del vecino.
    /// </summary>
    [Fact]
    public void ACase_ShouldCarryTheDataTableItCites_AndNotTheNeighbours()
    {
        var blocks = TestSuiteIndex.BlocksOf("Sintetico.cs", """
            public class ConCasos
            {
                private static TheoryData<string, string> LosCasos => new()
                {
                    { "NodoDePrueba", "Out" }
                };

                private static TheoryData<string> LaDelVecino => new() { "ajena" };

                [Theory]
                [MemberData(nameof(LosCasos))]
                public void UnCasoTeorico(string nodo, string puerto)
                {
                    Assert.NotNull(nodo);
                }

                [Fact]
                public void OtroCaso()
                {
                    Assert.True(true);
                }
            }
            """);

        blocks.Should().HaveCount(2);

        var teorico = blocks.Single(b => b.Name == "UnCasoTeorico");
        teorico.Source.Should().Contain("LosCasos").And.Contain("NodoDePrueba").And.Contain("\"Out\"",
            "la tabla citada es la evidencia del caso: sin ella no podría nombrar ni el nodo ni el puerto");
        teorico.Source.Should().NotContain("LaDelVecino", "la tabla de un vecino no cuenta para nadie");

        blocks.Single(b => b.Name == "OtroCaso").Source.Should().NotContain("UnCasoTeorico",
            "el bloque se corta en el final del cuerpo, no en la declaración siguiente");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El índice sobre el árbol real
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>La guardia contra la ceguera</b>: ningún fichero del suite que declare un caso puede quedarse sin
    /// casos. Es la comprobación que habría cazado al instante los dos fallos que ya ocurrieron —el retroceso
    /// catastrófico dejaba el índice a medias y el analizador de comentarios pegado las líneas de los 158 ficheros
    /// en CRLF, y ambos se descubrieron por casualidad, mirando otro problema—.
    /// </summary>
    [Fact]
    public void NoFileThatDeclaresACase_ShouldComeBackEmpty()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var silent = new List<string>();
        var blocks = new List<TestSuiteIndex.TestBlock>();

        foreach ((string file, string source) in SourceTree.TestFiles(root))
        {
            bool declares = source.Contains("[Fact", StringComparison.Ordinal)
                            || source.Contains("[Theory", StringComparison.Ordinal);

            var read = TestSuiteIndex.BlocksOf(file, source);

            if (declares && read.Count == 0) silent.Add(file);
            blocks.AddRange(read);
        }

        silent.Should().BeEmpty(
            "si un fichero que declara casos no devuelve ninguno, el índice está mirando otra cosa; y una guardia " +
            "que cita pruebas como evidencia pasaría en verde sin haberlas leído");

        blocks.Count.Should().BeGreaterThan(1000,
            "el suite tiene más de mil casos declarados: una cifra muy por debajo significa que el analizador dejó " +
            "de ver una parte del árbol");
    }

    /// <summary>
    /// Y el techo de tiempo, porque el fallo caro no fue ver mal sino <b>tardar</b>: la primera redacción de la
    /// búsqueda de declaraciones era una expresión regular con retroceso catastrófico y tardaba <b>103 s en un solo
    /// fichero de 19 KB</b> —el censo de puertos entero se iba a 1 m 46 s—, de modo que la guardia dejó de
    /// ejecutarse. El margen es de doscientas veces lo medido (decenas de milisegundos), así que no depende de la
    /// carga de la máquina y sigue mordiendo si vuelve un análisis cuadrático.
    /// </summary>
    [Fact]
    public void ReadingTheWholeSuite_ShouldCostMilliseconds()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var watch = Stopwatch.StartNew();

        int count = TestSuiteIndex.Blocks(root).Count;

        watch.Stop();
        count.Should().BeGreaterThan(1000);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15),
            "leer el suite entero cuesta decenas de milisegundos: medio minuto aquí es un análisis que volvió a " +
            "hacerse cuadrático, y una guardia que tarda eso se acaba no ejecutando");
    }
}
