using System;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
using FileFlow.Sdk.Localization;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Registra el <b>diccionario de recursos del host antes de que corra ningún test</b>.
///
/// <para><b>Por qué existe</b> (hito 179, medido): <see cref="LocalizationManager.GetString"/> recorre los
/// gestores registrados y devuelve el primero que tenga la clave; si no hay ninguno, devuelve <i>la clave</i>.
/// El diccionario del host se registraba de forma perezosa —la primera vez que una prueba preparaba la sesión
/// headless, a mitad del suite y en paralelo con las demás—, así que durante la suite una misma clave
/// cambiaba de valor <b>una vez</b>: medido con una sonda, <c>'LogStartingExecution'</c> al cargar el módulo y
/// <c>'--- Iniciando Ejecución ---'</c> diez segundos después (con el gestor del host ya entre los
/// registrados). Cualquier prueba que resuelva esa clave a los dos lados de ese instante compara dos textos
/// distintos y falla: raro, dependiente del orden de ejecución y <b>invisible en una ejecución filtrada</b>
/// —donde nadie registra nada y la clave se resuelve a sí misma las dos veces—, que es la peor forma de
/// fallar. Era la causa de que <c>WorkflowExecutionThroughTheAppTests</c> fallara de vez en cuando en su
/// aserción de la consola: el mensaje de arranque se resuelve al encolarlo (dentro de la ejecución) y otra
/// vez al afirmar, y un registro ajeno en medio los separaba.</para>
///
/// <para><b>Lo que hace</b>: registrar el diccionario del host en la inicialización del módulo, es decir
/// antes de que exista un test. El valor de una clave del host pasa a ser el mismo desde el primer test hasta
/// el último y la ventana de la carrera queda vacía. Es además lo que hace la aplicación de verdad: su
/// arranque también registra el diccionario, así que no hay un momento de la vida del proceso en el que el
/// host no tenga sus cadenas.</para>
///
/// <para><b>Lo que no cubre</b>: los recursos de los <i>plugins</i> siguen registrándose cuando cada plugin se
/// carga (como en producción), así que una clave de plugin todavía puede cambiar de valor a mitad del suite.
/// Una prueba que necesite comparar una cadena de plugin en dos momentos tiene que registrar ese plugin
/// antes, como hace <see cref="ModalVisualFixture"/> con las capturas. El idioma tampoco se fija aquí: lo fija
/// <see cref="AvaloniaTestHelper"/> en cada preparación de la sesión, y las capturas lo fijan y lo restauran
/// alrededor de cada imagen.</para>
/// </summary>
public static class HostLocalization
{
    /// <summary>Nombre base del diccionario de recursos del host (el mismo que registra su arranque).</summary>
    public const string HostResourceBaseName = "FileFlow.App.Resources.Strings";

    private static readonly Lock Gate = new();
    private static bool _registered;

    /// <summary>
    /// El arranque del suite: se ejecuta al cargar el módulo, antes de que xUnit descubra o corra nada.
    /// </summary>
    [ModuleInitializer]
    internal static void InitializeBeforeAnyTest() => EnsureRegistered();

    /// <summary>¿Se registró ya el diccionario del host? Lo comprueba la guardia del arranque.</summary>
    public static bool IsRegistered
    {
        get
        {
            lock (Gate)
            {
                return _registered;
            }
        }
    }

    /// <summary>Idempotente: registrarlo dos veces es un no-op (el gestor se deduplica).</summary>
    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            LocalizationManager.Instance.RegisterResourceManager(NewHostResourceManager());
            _registered = true;
        }
    }

    /// <summary>
    /// Un gestor <b>nuevo</b> del diccionario del host. Existe para que la guardia del arranque pueda
    /// reproducir el registro perezoso —el que ocurría a mitad del suite— y comprobar que el valor de una
    /// clave no depende de si ya se registró.
    /// </summary>
    public static ResourceManager NewHostResourceManager() =>
        new(HostResourceBaseName, typeof(FileFlow.App.App).Assembly);
}
