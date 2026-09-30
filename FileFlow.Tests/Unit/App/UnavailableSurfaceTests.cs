using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La costura que declara las superficies del toolkit de la versión anterior (hito 268), medida por su
/// <b>comportamiento</b> y no por su texto: un nodo compilado para el host Uno no puede construir su ventana,
/// así que la frontera se dice por los diálogos de quien lo abrió.
///
/// <para><b>Qué protege.</b> Que declarar la frontera <i>diga algo</i>. La mitad del defecto que este hito
/// cierra era justamente un botón que no hacía nada y no avisaba: si esta costura dejara de avisar —o avisara
/// por un canal que el usuario no ve—, el host Uno volvería a tener puertas mudas, y el resto de la suite no
/// lo notaría (los nodos siguen declarando la frontera en su código).</para>
/// </summary>
public class UnavailableSurfaceTests
{
    [Fact]
    public void DeclaringTheFrontier_ShouldShowItInTheHostDialogs()
    {
        var dialogs = new RecordingDialogService();

        UnavailableSurface.Declare(
            dialogs,
            "Gestor de Contraseñas",
            "Función no disponible",
            "«Gestor de Contraseñas» no está disponible en este host.");

        dialogs.WarningMessages.Should().ContainSingle(
            "la frontera se dice UNA vez, en el canal de avisos: quien la alcanza se entera de qué se perdió")
            .Which.Should().Contain("Gestor de Contraseñas",
                "y el aviso lleva el nombre de la superficie: sin él, el usuario sabe que algo falló pero no qué");
    }

    [Fact]
    public void DeclaringTheFrontier_ShouldNeverBlowUp_WithoutDialogs()
    {
        // Un host sin diálogos (una prueba, un contexto sin ventana) no puede convertir la frontera en una
        // excepción: el nodo sigue su camino y la traza queda escrita.
        var act = () => UnavailableSurface.Declare(null, "X", "Y", "Z");

        act.Should().NotThrow();
    }
}
