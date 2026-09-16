using System;
using System.Collections.Generic;
using System.Resources;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using FileFlow.Sdk.Localization;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Infraestructura headless de los tests: un único bucle de mensajes de Avalonia para todo el suite.
///
/// Por qué no basta con <c>SetupWithoutStarting</c>: aquel arranque dejaba Avalonia inicializada en el hilo
/// que llegara primero. Cualquier otro hilo que tocara la UI después competía por el Dispatcher, el
/// <c>Application.Current</c> podía quedar a medias y el helper se lo tragaba con un <c>try/catch</c> vacío,
/// de modo que los tests fallaban más tarde y en otro sitio. Aquí se usa
/// <see cref="HeadlessUnitTestSession"/>, que arranca la aplicación en un hilo dedicado con su propio
/// bucle: el trabajo de UI se despacha allí de forma serializada y **los fallos de arranque se propagan**
/// con su excepción original.
///
/// Además del arranque, el helper impone un <b>contrato de hilo</b> explícito. Es lo que separa un test de
/// UI fiable de uno que pasa por casualidad: un <see cref="Control"/> construido en el hilo del runner no
/// tiene afinidad comprobada —funciona hasta que Avalonia decide tocar la plataforma de ventanas— y las
/// consultas al diccionario del tema devuelven <c>null</c> en silencio porque no hay aplicación en ese
/// hilo. Las tres reglas son:
/// <list type="number">
///   <item>Todo lo que cree o modifique controles pasa por <see cref="RunOnUI(Action)"/>.</item>
///   <item>Construir controles fuera de ese hilo lanza una excepción que dice qué hacer
///   (<see cref="RequireUIThread"/>), en lugar de un <c>NullReferenceException</c> ajeno al test.</item>
///   <item>Un despacho anidado se ejecuta en línea en vez de bloquear el bucle (que sería un interbloqueo
///   silencioso hasta el <c>timeout</c> del runner).</item>
/// </list>
///
/// La sesión se prepara una sola vez y de forma explícita:
/// <list type="bullet">
///   <item>Se neutralizan las animaciones de los estilos: Avalonia 12 no expone un animador público para
///   <c>RenderTransform</c>, así que adjuntar cualquier estilo que anime esa propiedad lanza
///   <c>InvalidOperationException</c> al construir un <c>TopLevel</c>. Además, una captura de pantalla con
///   animaciones en vuelo no es determinista.</item>
///   <item>Se registra el diccionario de recursos del host (lo hace <c>App.OnFrameworkInitializationCompleted</c>,
///   que en headless no llega a ejecutar su rama de escritorio) y se fija el idioma, para que los textos
///   renderizados no dependan del entorno.</item>
/// </list>
/// </summary>
public static class AvaloniaTestHelper
{
    /// <summary>Idioma fijo de las capturas: el resultado no puede depender del entorno.</summary>
    public const string PinnedLanguage = "es-ES";

    private static readonly Lock Gate = new();

    private static HeadlessUnitTestSession? _session;

    /// <summary>
    /// Aplicación ya preparada. Es una referencia y no un booleano a propósito: la sesión puede recrear la
    /// aplicación (aislamiento por clase de test) y una instancia nueva necesita que se le desactiven las
    /// animaciones otra vez.
    /// </summary>
    private static Application? _preparedApplication;

    /// <summary>¿Se registró ya el diccionario de recursos del host? Es idempotente y de una sola vez por proceso.</summary>
    private static bool _hostResourcesRegistered;

    /// <summary>
    /// Marca de «este hilo es el hilo de UI de la sesión». Es <c>[ThreadStatic]</c> y no
    /// <c>Dispatcher.UIThread.CheckAccess()</c> a propósito: antes de que la sesión arranque, Avalonia crea
    /// un Dispatcher para el hilo que pregunte, así que <c>CheckAccess()</c> diría «sí» sobre el hilo del
    /// runner. La marca sólo la pone el propio despacho, de modo que no puede dar falsos positivos.
    /// </summary>
    [ThreadStatic]
    private static bool _isSessionUiThread;

    /// <summary>¿Está lista la sesión headless? Sólo se inicializa cuando un test la necesita.</summary>
    public static bool IsInitialized
    {
        get
        {
            lock (Gate)
            {
                return _session != null;
            }
        }
    }

    /// <summary>
    /// ¿Se está ejecutando esto ya dentro del despacho de UI de la sesión? Es la comprobación que deben usar
    /// los helpers que construyen controles (galería, capturas, fixtures) para fallar pronto y claro.
    /// </summary>
    public static bool IsOnUIThread => _isSessionUiThread;

    /// <summary>
    /// Exige estar en el hilo de UI de la sesión. Cualquier helper que construya o inspeccione controles debe
    /// llamarlo primero: el mensaje de error explica cómo arreglarlo, mientras que sin él el fallo aparecería
    /// más tarde y con una excepción que no menciona el hilo («Unable to locate IWindowingPlatform», recursos
    /// del tema que devuelven <c>null</c>…).
    /// </summary>
    public static void RequireUIThread(string operation)
    {
        if (_isSessionUiThread)
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{operation}' crea o inspecciona controles de Avalonia y debe ejecutarse en el hilo de UI de la " +
            "sesión headless. Envuélvelo en AvaloniaTestHelper.RunOnUI(() => ...) o usa la sobrecarga de " +
            "VisualSnapshot.Capture que recibe una fábrica: la fábrica se invoca ya dentro del hilo correcto.");
    }

    /// <summary>
    /// Arranca la sesión headless si no lo estaba. Lanza si Avalonia no puede inicializarse: un fallo de
    /// infraestructura debe verse como tal, no convertirse en un test que falla por una razón ajena.
    /// </summary>
    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_session != null)
            {
                return;
            }

            // GetOrStartForAssembly (y no StartNew) porque es la vía que respeta el
            // [AvaloniaTestApplication] declarado en este ensamblado, y es lo que habilita Skia real.
            _session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTestHelper).Assembly);
        }
    }

    /// <summary>
    /// Cambia el idioma del <see cref="LocalizationManager"/> desde el hilo de UI de la sesión.
    ///
    /// En producción la cultura sólo se cambia desde la interfaz (selector de idioma, Theme Studio), así
    /// que los bindings de los controles reevalúan siempre en el hilo que posee los objetos y
    /// <c>VerifyAccess</c> pasa. Un test que llama <c>SetCulture</c> desde el hilo del runner dispara en
    /// cambio los <c>LanguageChanged</c>/<c>PropertyChanged</c> residuales de árboles ya desmontados
    /// (aunque estén purgados, Avalonia conserva vinculaciones del chrome de la ventana hasta que el GC
    /// recolecta) contra controles propiedad del hilo de UI: la notificación explota con
    /// «The calling thread cannot access this object» y mata al test que la provoca. Marshaling: la
    /// notificación corre donde debe.
    /// </summary>
    public static void SetCultureOnUI(string cultureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureCode);

        // Fiel a 'SetCulture': siempre dispara 'LanguageChanged' aunque el idioma ya fuera el pedido.
        // Los tests alternan en-US/es-ES y algunos asercan que el evento saltó: el filtro del pin de
        // sesión (ver EnsureSessionLanguage) vive sólo en el arranque, no aquí.
        RunOnUI(() => LocalizationManager.Instance.SetCulture(cultureCode));
    }

    /// <summary>Ejecuta una acción en el hilo de UI de la sesión y espera a que termine.</summary>
    public static void RunOnUI(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dispatch(() =>
        {
            action();
            return true;
        });
    }

    /// <summary>Ejecuta una función en el hilo de UI de la sesión y devuelve su resultado.</summary>
    public static T RunOnUI<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Dispatch(action);
    }

    /// <summary>
    /// Despacha trabajo al hilo de UI. Es seguro llamarlo desde varios hilos a la vez (el bucle de la sesión
    /// los serializa).
    ///
    /// Un despacho anidado —llamar aquí desde dentro de otro despacho, algo que ocurre en cuanto un helper
    /// público se usa desde dentro de una fábrica— se ejecuta <b>en línea</b>: encolarlo esperando su
    /// resultado bloquearía el bucle que tiene que atenderlo, y el test moriría con un interbloqueo tras el
    /// tiempo de espera del runner en lugar de con una traza.
    /// </summary>
    public static T Dispatch<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_isSessionUiThread)
        {
            // Ya estamos dentro del bucle: ejecutar directamente.
            return action();
        }

        EnsureInitialized();

        HeadlessUnitTestSession session;

        lock (Gate)
        {
            session = _session!;
        }

        return session.Dispatch(
            () =>
            {
                _isSessionUiThread = true;

                try
                {
                    PrepareApplication();
                    return action();
                }
                finally
                {
                    _isSessionUiThread = false;
                }
            },
            CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Ajustes de sesión que deben ocurrir ya en el hilo de UI, una sola vez:
    /// silenciar animaciones y dejar la localización en un estado conocido.
    /// </summary>
    private static void PrepareApplication()
    {
        var application = Application.Current
            ?? throw new InvalidOperationException(
                "La sesión headless no tiene 'Application.Current': revisa FileFlowTestAppBuilder, la aplicación " +
                "de pruebas no llegó a arrancar.");

        if (ReferenceEquals(application, _preparedApplication))
        {
            return;
        }

        DisableStyleAnimations(application.Styles);
        RegisterHostResources();
        _preparedApplication = application;
    }

    /// <summary>
    /// Quita las animaciones declaradas en los estilos (incluidas las anidadas y los <c>ControlTheme</c>).
    /// Sin ellas el árbol no busca un animador de <c>RenderTransform</c> —inexistente como API pública en
    /// Avalonia 12— y la captura no depende del instante en que se tomó.
    /// </summary>
    private static void DisableStyleAnimations(IEnumerable<IStyle> styles)
    {
        foreach (var style in styles)
        {
            switch (style)
            {
                case Style concrete:
                    concrete.Animations?.Clear();
                    DisableStyleAnimations(concrete.Children);
                    DisableStyleAnimationsInResources(concrete.Resources);
                    break;

                case ControlTheme controlTheme:
                    controlTheme.Animations?.Clear();
                    DisableStyleAnimations(controlTheme.Children);
                    DisableStyleAnimationsInResources(controlTheme.Resources);
                    break;

                case Styles group:
                    DisableStyleAnimations(group);
                    DisableStyleAnimationsInResources(group.Resources);
                    break;
            }
        }
    }

    private static void DisableStyleAnimationsInResources(IResourceDictionary? resources)
    {
        if (resources == null)
        {
            return;
        }

        foreach (var entry in resources)
        {
            switch (entry.Value)
            {
                case ControlTheme theme:
                    theme.Animations?.Clear();
                    DisableStyleAnimations(theme.Children);
                    break;

                case Styles group:
                    DisableStyleAnimations(group);
                    break;
            }
        }
    }

    /// <summary>
    /// Registra el diccionario de recursos del host (el código de arranque de la aplicación no se ejecuta en
    /// headless) y fija el idioma, de forma que los textos de las vistas no queden vacíos ni dependan de la
    /// cultura del equipo.
    /// </summary>
    private static void RegisterHostResources()
    {
        var localization = LocalizationManager.Instance;

        // Sin borrar los diccionarios ya registrados: hacerlo era una operación global destructiva que dejaba
        // sin traducir a cualquier prueba que hubiera registrado el suyo (los plugins registran los propios
        // al cargarse), y el fallo aparecía en otra prueba y sólo al ejecutar el suite entero.
        // 'RegisterResourceManager' ya ignora los duplicados, así que basta con no repetirlo.
        if (!_hostResourcesRegistered)
        {
            localization.RegisterResourceManager(
                new ResourceManager("FileFlow.App.Resources.Strings", typeof(FileFlow.App.App).Assembly));

            _hostResourcesRegistered = true;
        }

        // Sólo se cambia de idioma si hace falta: 'SetCulture' dispara 'LanguageChanged' y con él los
        // refrescos de la interfaz, así que no se provoca sin motivo.
        if (!string.Equals(localization.CurrentLanguage, PinnedLanguage[..2], StringComparison.OrdinalIgnoreCase))
        {
            localization.SetCulture(PinnedLanguage);
        }
    }
}
