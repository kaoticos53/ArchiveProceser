using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La <b>cobertura publicada</b> de las mutaciones declaradas ([`mutations/COVERAGE.md`](file:///mutations/COVERAGE.md)):
/// qué declara cada una, qué subsistemas del producto <b>no</b> tienen ninguna y qué guardias del repositorio
/// nadie ha demostrado que muerdan.
///
/// <para>Es el mismo trato que el catálogo de nodos: el documento se <b>genera</b> desde lo declarado y el árbol,
/// y esta guardia falla si el fichero publicado no coincide con lo que dicen las declaraciones. Así la lista de
/// huecos no se queda quieta mientras el catálogo crece —que es el único modo de que una lista de trabajo siga
/// sirviendo— y nadie la edita a mano.</para>
///
/// <para>Regenerar: <c>FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests</c>.</para>
/// </summary>
public class MutationDeclarationCoverageTests
{
    private const string RegenerateVariable = "FILEFLOW_UPDATE_MUTATION_COVERAGE";
    private static readonly string DocumentPath = Path.Combine("mutations", "COVERAGE.md");

    [Fact]
    public void ThePublishedCoverage_ShouldMatchTheDeclarationsAndTheTree()
    {
        // Arrange
        string root = TestRepositoryLocator.RepositoryRoot();
        var declarations = MutationDeclarationAudit.Declarations(root);
        string rendered = MutationDeclarationAudit.RenderCoverage(root, declarations);
        string path = Path.Combine(root, DocumentPath);

        // Act
        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            File.WriteAllText(path, rendered);
        }

        // Assert
        File.Exists(path).Should().BeTrue(
            "la cobertura se publica en '{0}': regenera con {1}=1", path, RegenerateVariable);
        File.ReadAllText(path).Should().Be(rendered,
            "el documento publicado no puede discrepar de lo declarado ni del árbol; regenera con {0}=1", RegenerateVariable);
    }

    [Fact]
    public void TheCoverage_ShouldPublishTheSubsystemsWithoutAnyMutation_NotJustTheCoveredOnes()
    {
        // Arrange: dos proyectos de producto (uno con mutación, otro sin) y una declaración sobre el suite
        var declarations = new List<MutationDeclarationAudit.Declaration>
        {
            Synthetic("mutacion-de-producto", "FileFlow.Core/Engine/Algo.cs"),
            Synthetic("mutacion-de-guardia", "FileFlow.Tests/TestHelpers/Inventario.cs"),
        };

        // Act
        var coverage = MutationDeclarationAudit.Coverage(["FileFlow.App", "FileFlow.Core"], declarations);

        // Assert
        coverage.Should().HaveCount(3, "una fila por proyecto del producto más el suite, con mutaciones o sin ellas");
        coverage.Single(row => row.Subsystem == "FileFlow.Core").Mutations.Should().Equal(new[] { "mutacion-de-producto" });
        coverage.Single(row => row.Subsystem == "FileFlow.App").Mutations.Should().BeEmpty(
            "un subsistema sin mutaciones es justo lo que hay que publicar");
        coverage.Single(row => !row.IsProduct).Mutations.Should().Equal(new[] { "mutacion-de-guardia" },
            "mutar la declaración de una guardia no cubre ningún subsistema del producto");
    }

    [Fact]
    public void TheRealCoverage_ShouldBeRenderedWithItsGaps_AndItsCounts()
    {
        // Arrange
        string root = TestRepositoryLocator.RepositoryRoot();
        var declarations = MutationDeclarationAudit.Declarations(root);

        // Act
        string rendered = MutationDeclarationAudit.RenderCoverage(root, declarations);
        var coverage = MutationDeclarationAudit.Coverage(MutationDeclarationAudit.ProductProjects(root), declarations);

        // Assert
        rendered.Should().Contain($"Mutaciones declaradas: {declarations.Count}",
            "la cabecera lleva el número que el andamiaje usa para saber si el documento está al día");
        rendered.Should().Contain("Subsistemas del producto sin ninguna mutación declarada");
        rendered.Should().Contain("Guardias del repositorio sin ninguna mutación que las muerda");
        MutationDeclarationAudit.ProductProjects(root).Should().NotBeEmpty("el producto tiene proyectos que cubrir");
        coverage.Should().Contain(row => row.IsProduct && row.Mutations.Count == 0,
            "con este catálogo, publicar los huecos es el resultado esperado, no una anomalía");
    }

    private static MutationDeclarationAudit.Declaration Synthetic(string id, string file) =>
        new(
            id,
            id + ".json",
            "tesis de sonda",
            [new MutationDeclarationAudit.Edit(file, [new MutationDeclarationAudit.Replacement("A();", "B();", 1)])],
            new MutationDeclarationAudit.Filter("FullyQualifiedName~NodeTests", null),
            null);
}
