using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de las <b>mutaciones declaradas</b> (<c>mutations/*.json</c>, las que ejecuta <c>mutate.ps1</c>).
///
/// <para>Una mutación afirma «este defecto lo detecta esta prueba». Esa afirmación se pudre sin hacer ruido: el
/// producto cambia y el fragmento que se sustituía desaparece, la prueba se renombra y su filtro deja de casar.
/// El andamiaje lo detecta cuando alguien ejecuta la mutación —y entonces <i>ya no está midiendo</i>: un filtro
/// que no casa con ninguna prueba deja a <c>dotnet test</c> salir con 0, así que un testigo renombrado se lee
/// como «la mutación sobrevivió» y un control renombrado como «control verde»—. Esta guardia lo detecta en el
/// suite, que es donde se mira a diario.</para>
///
/// <para>El análisis vive en <see cref="MutationDeclarationAudit"/> y se auto-testea aquí con declaraciones
/// sintéticas: una guardia que audita texto tiene que demostrar que muerde.</para>
/// </summary>
public class MutationDeclarationGuardTests
{
    [Fact]
    public void EveryDeclaredMutation_ShouldStillFitTheProductAndTheSuite()
    {
        // Arrange
        string root = TestRepositoryLocator.RepositoryRoot();
        var declarations = MutationDeclarationAudit.Declarations(root);

        // Act
        var infractions = MutationDeclarationAudit.Audit(
            declarations,
            relativePath => File.Exists(Path.Combine(root, relativePath))
                ? File.ReadAllText(Path.Combine(root, relativePath))
                : null,
            MutationDeclarationAudit.SuiteNames(root));

        // Assert
        infractions.Should().BeEmpty(
            "una mutación declarada que ya no encaja miente sobre lo que el suite vigila: {0}",
            string.Join(" | ", infractions));
    }

    [Fact]
    public void TheDeclarations_ShouldBeDiscoveredFromTheRepositoryFolder_WithUniqueIdentifiers()
    {
        // Arrange
        string root = TestRepositoryLocator.RepositoryRoot();

        // Act
        var declarations = MutationDeclarationAudit.Declarations(root);

        // Assert
        declarations.Should().NotBeEmpty(
            "el andamiaje de mutaciones no sirve de nada sin definiciones declaradas en mutations/");
        declarations.Select(d => d.Id).Should().OnlyHaveUniqueItems("el id es lo que se pasa a `mutate.ps1 -Name`");
        declarations.Should().OnlyContain(d => d.Edits.Count > 0 && d.Edits.All(e => e.Replacements.Count > 0),
            "una mutación sin sustituciones no muta nada");
    }

    [Fact]
    public void TheAudit_ShouldCatchAFragmentThatNoLongerExists()
    {
        // Arrange: la declaración espera el fragmento una vez y el producto ya no lo tiene
        var declaration = Synthetic(
            edits: [new MutationDeclarationAudit.Edit("FileFlow.Prod/Node.cs", [new("await storage.DeleteAsync(path)", "Directory.Delete(path)", 1)])],
            witness: new MutationDeclarationAudit.Filter("FullyQualifiedName~NodeTests", null));

        // Act
        var infractions = Audit(declaration, new Dictionary<string, string>
        {
            ["FileFlow.Prod/Node.cs"] = "public class Node\n{\n    // el borrado se hace por el contrato\n}\n",
        });

        // Assert
        infractions.Should().ContainSingle().Which.Should().Contain("aparece 0 vez/veces")
            .And.Contain("FileFlow.Prod/Node.cs", "la infracción tiene que decir dónde");
    }

    [Fact]
    public void TheAudit_ShouldCatchAFragmentThatAppearsMoreTimesThanDeclared()
    {
        // Arrange: dos apariciones y una declarada es una sustitución que ya no es la que era
        var declaration = Synthetic(
            edits: [new MutationDeclarationAudit.Edit("FileFlow.Prod/Node.cs", [new("return true;", "return false;", 1)])],
            witness: new MutationDeclarationAudit.Filter("FullyQualifiedName~NodeTests", null));

        // Act
        var infractions = Audit(declaration, new Dictionary<string, string>
        {
            ["FileFlow.Prod/Node.cs"] = "bool A() { return true; }\nbool B() { return true; }\n",
        });

        // Assert
        infractions.Should().ContainSingle().Which.Should().Contain("aparece 2 vez/veces y la mutación declara 1");
    }

    [Fact]
    public void TheAudit_ShouldCatchAWitnessThatNoLongerExistsInTheSuite()
    {
        // Arrange: la prueba citada ya no está (el filtro no casa con ninguna y el andamiaje lo leería como
        // «superviviente», o como «control verde» si fuera el control)
        var declaration = Synthetic(
            edits: [new MutationDeclarationAudit.Edit("FileFlow.Prod/Node.cs", [new("A();", "B();", 1)])],
            witness: new MutationDeclarationAudit.Filter("FullyQualifiedName~LaPruebaDeSiempre", null));

        // Act
        var infractions = Audit(declaration, new Dictionary<string, string> { ["FileFlow.Prod/Node.cs"] = "A();\n" });

        // Assert
        infractions.Should().ContainSingle().Which.Should().Contain("LaPruebaDeSiempre")
            .And.Contain("no casa con ninguna prueba del suite");
    }

    [Fact]
    public void TheAudit_ShouldAcceptAFilterThatCitesOnlyTheBeginningOfAName()
    {
        // El '~' de `dotnet test` es coincidencia parcial: citar el principio del método de una teoría —sin su
        // sufijo— es legítimo, y el auditor no puede exigir el nombre entero o rechazaría testigos que sí miden.
        var declaration = Synthetic(
            edits: [new MutationDeclarationAudit.Edit("FileFlow.Prod/Node.cs", [new("A();", "B();", 1)])],
            witness: new MutationDeclarationAudit.Filter("FullyQualifiedName~NodeTests_WhenSomethingHappens", null));

        // Act
        var infractions = MutationDeclarationAudit.Audit(
            [declaration],
            path => path == "FileFlow.Prod/Node.cs" ? "A();\n" : null,
            new HashSet<string>(["NodeTests_WhenSomethingHappens_ShouldLeaveByTheDeclaredPort"], StringComparer.Ordinal));

        // Assert
        infractions.Should().BeEmpty();
    }

    [Fact]
    public void TheAudit_ShouldCatchADeclarationWithoutWitness()
    {
        // Arrange
        var declaration = Synthetic(
            edits: [new MutationDeclarationAudit.Edit("FileFlow.Prod/Node.cs", [new("A();", "B();", 1)])],
            witness: null);

        // Act
        var infractions = Audit(declaration, new Dictionary<string, string> { ["FileFlow.Prod/Node.cs"] = "A();\n" });

        // Assert
        infractions.Should().ContainSingle().Which.Should().Contain("no declara testigo");
    }

    [Fact]
    public void TheAudit_ShouldNotBeFooledByLineEndings()
    {
        // Arrange: el fragmento se declara en LF y el producto está en CRLF (el caso del repositorio)
        var declaration = Synthetic(
            edits: [new MutationDeclarationAudit.Edit("FileFlow.Prod/Node.cs", [new("if (a)\n{\n    b();\n}", "c();", 1)])],
            witness: new MutationDeclarationAudit.Filter("FullyQualifiedName~NodeTests", null));

        // Act
        var infractions = Audit(declaration, new Dictionary<string, string>
        {
            ["FileFlow.Prod/Node.cs"] = "if (a)\r\n{\r\n    b();\r\n}\r\n",
        });

        // Assert
        infractions.Should().BeEmpty("el andamiaje compara con los terminadores normalizados antes de sustituir");
    }

    [Fact]
    public void TheAudit_ShouldCatchAnIdentifierThatDoesNotMatchItsFile()
    {
        // Arrange: el id es lo que se pasa a `-Name` y el fichero lo que se lee
        var declaration = MutationDeclarationAudit.Declarations(TestRepositoryLocator.RepositoryRoot()).First() with
        {
            Id = "otro-id",
        };

        // Act
        var infractions = MutationDeclarationAudit.Audit(
            [declaration],
            relativePath => File.Exists(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath))
                ? File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath))
                : null,
            MutationDeclarationAudit.SuiteNames(TestRepositoryLocator.RepositoryRoot()));

        // Assert
        infractions.Should().Contain(i => i.Contains("no coincide con el nombre del fichero"));
    }

    private static MutationDeclarationAudit.Declaration Synthetic(
        IReadOnlyList<MutationDeclarationAudit.Edit> edits,
        MutationDeclarationAudit.Filter? witness) =>
        new("mutacion-de-sonda", "mutacion-de-sonda.json", "tesis de sonda", edits, witness, null);

    private static IReadOnlyList<string> Audit(
        MutationDeclarationAudit.Declaration declaration,
        IReadOnlyDictionary<string, string> files) =>
        MutationDeclarationAudit.Audit(
            [declaration],
            path => files.TryGetValue(path, out string? content) ? content : null,
            new HashSet<string>(["NodeTests_WhenSomethingHappens_ShouldLeaveByTheDeclaredPort", "TestSuiteIndexTests"], StringComparer.Ordinal));
}
