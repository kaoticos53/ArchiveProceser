using FileFlow.App.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La regla que decide qué puerto vigente se parece más al que falta. Es lo que convierte «no se pudo
/// reconstruir la conexión» en un botón, así que lo que se fija aquí es tan importante como la propuesta: sus
/// <b>límites</b> —cuándo no hay propuesta— y el desempate.
/// </summary>
public class PortNameProposalTests
{
    [Theory]
    [InlineData("Alternate", "Alternates", 0.9)]     // un sufijo
    [InlineData("Errores", "Error", 0.71)]           // una errata
    [InlineData("Alternate", "Out", 0.11)]           // nada que ver
    [InlineData("Out", "out", 1.0)]                  // el caso no distingue mayúsculas
    public void Similarity_ShouldMeasureHowCloseTwoNamesAre(string missing, string candidate, double expected)
    {
        PortNameProposal.Similarity(missing, candidate).Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public void Suggest_ShouldPickTheClosestCandidate()
    {
        PortNameProposal.Suggest("Alternate", ["In", "Out", "Alternates"]).Should().Be("Alternates");
    }

    /// <summary>
    /// Ante un empate gana el primero que aparece, que es el orden en el que el usuario ve los puertos en la
    /// tarjeta: una propuesta que cambiara de puerto entre dos ejecuciones idénticas sería una lotería.
    /// </summary>
    [Fact]
    public void Suggest_ShouldKeepTheFirstWhenTwoCandidatesAreEquallyClose()
    {
        PortNameProposal.Suggest("Ab", ["Aa", "Ac"]).Should().Be("Aa");
    }

    /// <summary>
    /// El límite que hace útil el botón: por debajo de la mitad del nombre no se propone nada. Proponer
    /// cualquier cosa antes que nada convierte la reconexión de un clic en una trampa de un clic.
    /// </summary>
    [Fact]
    public void Suggest_ShouldProposeNothingWhenNoCandidateIsCloseEnough()
    {
        PortNameProposal.Suggest("Alternate", ["In", "Out", "Salida"]).Should().BeNull();
    }

    /// <summary>Justo en el umbral sí se propone: el umbral es «no por debajo de la mitad», no «más de la mitad».</summary>
    [Fact]
    public void Suggest_ShouldProposeAtTheThreshold()
    {
        PortNameProposal.Similarity("Ab", "Aa").Should().Be(PortNameProposal.MinimumSimilarity);
        PortNameProposal.Suggest("Ab", ["Aa"]).Should().Be("Aa");
    }

    [Fact]
    public void Suggest_ShouldProposeNothingWithoutACandidateList()
    {
        PortNameProposal.Suggest("Alternate", []).Should().BeNull();
        PortNameProposal.Suggest("   ", ["Alternates"]).Should().BeNull();
    }
}
