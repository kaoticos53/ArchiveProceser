using FileFlow.Sdk.Localization;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del <b>estado de localización del proceso en el arranque del suite</b>: el diccionario de recursos
/// del host tiene que estar registrado antes de que corra ningún test.
///
/// <para><b>Qué protege</b> (hito 179, medido): el valor de una clave del host dependía de <i>cuándo</i> se
/// registraba su diccionario. Registrado de forma perezosa —la primera preparación de la sesión headless, a
/// mitad del suite—, la clave se resolvía a sí misma durante los primeros segundos y a su texto traducido
/// después. Una aserción que resuelve la clave dos veces (el mensaje de arranque de una ejecución: el
/// coordinador al encolarlo y la prueba al afirmarlo) comparaba entonces dos textos distintos, sólo a veces y
/// sólo en el suite completo. Con el registro en el arranque del módulo el valor es el mismo desde el primer
/// test, y estas dos comprobaciones lo dejan atado: la primera exige que el registro ya haya ocurrido, la
/// segunda que volver a registrarlo no cambie lo que resuelve una clave.</para>
/// </summary>
public class HostLocalizationBootstrapTests
{
    /// <summary>La clave que compara la prueba de la consola: el mensaje de arranque de una ejecución.</summary>
    private const string StartMessageKey = "LogStartingExecution";

    [Fact]
    public void TheHostDictionary_ShouldBeRegisteredBeforeAnyTest()
    {
        HostLocalization.IsRegistered.Should().BeTrue(
            "el diccionario del host se registra al cargar el módulo (HostLocalization.InitializeBeforeAnyTest): " +
            "si deja de hacerlo, su primera preparación llega a mitad del suite y una misma clave cambia de valor " +
            "mientras otras pruebas corren");

        LocalizationManager.Instance[StartMessageKey].Should().NotBe(
            StartMessageKey,
            "una clave del host tiene que resolverse a su texto desde el primer test; si se resuelve a sí misma " +
            "es que su diccionario todavía no está registrado y todavía puede cambiar de valor más adelante");
    }

    [Fact]
    public void ResolvingAHostKey_ShouldNotDependOnWhenTheHostDictionaryGetsRegistered()
    {
        string before = LocalizationManager.Instance[StartMessageKey];

        // El registro perezoso, tal cual lo hacía la preparación de la sesión headless: un gestor nuevo (el
        // servicio deduplica por instancia, no por nombre).
        LocalizationManager.Instance.RegisterResourceManager(HostLocalization.NewHostResourceManager());

        LocalizationManager.Instance[StartMessageKey].Should().Be(
            before,
            "registrar el diccionario del host no puede cambiar lo que una clave resuelve: la aserción de la " +
            "consola resuelve la suya dos veces —al encolarla el coordinador y al afirmarla la prueba— y un " +
            "cambio en medio las separa (hito 179)");
    }
}
