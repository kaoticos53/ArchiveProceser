using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Captura y compara imágenes de la interfaz para detectar regresiones visuales.
///
/// Cómo funciona:
/// <list type="bullet">
///   <item>El control se monta en una ventana sin decoración, con escalado 1:1 y el tema indicado, se muestra
///   (para que se apliquen los estilos y se haga el layout real) y se captura el fotograma renderizado por
///   Skia. La codificación a PNG y el guardado en bytes se hacen <b>dentro</b> del hilo de UI: los mapas de
///   bits tienen afinidad de hilo.</item>
///   <item>La comparación con la línea base admite una tolerancia (los antialias y las fuentes pueden variar
///   entre máquinas y versiones de Skia) pero acota el porcentaje de píxeles distintos, de forma que un
///   cambio de color, radio, espaciado o tamaño de letra se detecta igualmente.</item>
///   <item>Cuando la comparación falla se escriben la imagen capturada, la esperada y un mapa de diferencias
///   en un directorio temporal, y la ruta se incluye en el mensaje del test.</item>
/// </list>
///
/// <b>El contenido se recibe como fábrica</b>, no como control ya construido: los controles de Avalonia se
/// crean en el hilo de UI de la sesión y construir el árbol en el hilo del runner «funciona» hasta que
/// Avalonia necesita la plataforma de ventanas o el diccionario del tema —y entonces el fallo aparece como un
/// error de infraestructura desconcertante en lugar de como el test que se equivocó—. Con una fábrica no hay
/// forma de equivocarse: se invoca dentro del hilo correcto.
///
/// <b>Regenerar líneas base</b>: ejecutar con <c>FILEFLOW_UPDATE_VISUALS=1</c>. Si una línea base no existe,
/// el test la crea y falla a propósito: así una imagen nueva pasa siempre por revisión humana en lugar de
/// quedar bendecida de forma automática.
/// </summary>
public static class VisualSnapshot
{
    /// <summary>Porcentaje máximo de píxeles que pueden diferir de la línea base antes de considerarla regresión.</summary>
    private const double AllowedDifferingPixelRatio = 0.005;

    /// <summary>Diferencia por canal (0-255) por debajo de la cual un píxel se considera igual.</summary>
    private const int ChannelTolerance = 12;

    private static readonly string BaselineDirectory =
        Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.Tests", "VisualBaselines");

    private static readonly string ArtifactDirectory =
        Path.Combine(Path.GetTempPath(), "fileflow-visual-artifacts");

    /// <summary>¿Se pidió regenerar las líneas base?</summary>
    public static bool UpdateRequested =>
        Environment.GetEnvironmentVariable("FILEFLOW_UPDATE_VISUALS") is "1" or "true" or "TRUE";

    /// <summary>Directorio de las líneas base (para mensajes de error).</summary>
    public static string BaselineDirectoryPath => BaselineDirectory;

    /// <summary>Ruta de una línea base concreta (para mensajes de error).</summary>
    public static string BaselinePath(string name) => Path.Combine(BaselineDirectory, $"{name}.png");

    /// <summary>
    /// Captura como PNG el contenido que devuelve la fábrica, usando el tema indicado.
    ///
    /// La fábrica se invoca ya en el hilo de UI de la sesión, así que puede crear controles, leer tokens del
    /// tema y tocar view models sin preocuparse por el hilo.
    /// </summary>
    public static byte[] Capture(Func<Control> contentFactory, int width, int height, string themeId)
        => CaptureCore(contentFactory, width, height, themeId);

    /// <summary>
    /// Igual que <see cref="Capture(Func{Control}, int, int, string)"/> pero para una <see cref="Window"/>
    /// real — la modal tal y como se instancia en producción: su XAML raíz, sus propiedades de ventana y
    /// todo su árbol, con el tema activo de la captura.
    ///
    /// La fábrica se invoca <b>dentro del mismo despacho</b> que el show y la captura, y no es un detalle
    /// menor: una ventana construida en un despacho y mostrada en otro posterior no renderiza fotograma en
    /// la sesión headless (<c>CaptureRenderedFrame</c> devuelve <c>null</c>) — el compositor no repite la
    /// petición de primer frame. Construir, mostrar y capturar en un único despacho es la única secuencia
    /// fiable, igual que hace <see cref="CaptureCore"/> con su contenido.
    ///
    /// La limpieza es incondicional: pase lo que pase durante la captura, la ventana queda purgada
    /// (bindings) y cerrada, y el tema previo restablecido — el suite corre en paralelo y una ventana viva
    /// con bindings es una carrera en potencia.
    /// </summary>
    public static byte[] CaptureWindow(Func<Window> windowFactory, string themeId)
    {
        ArgumentNullException.ThrowIfNull(windowFactory);

        return AvaloniaTestHelper.RunOnUI(() =>
        {
            AvaloniaTestHelper.RequireUIThread("VisualSnapshot.CaptureWindow");

            var theme = ResolveTheme(themeId);
            string previousThemeId = ThemeManager.Instance.CurrentThemeId;

            // Mismo pin de idioma que CaptureCore: la ventana se construye (y sus textos se resuelven)
            // dentro de este despacho, y otra colección exclusiva puede estar en su hueco 'en-US'.
            string previousLanguage = FileFlow.Sdk.Localization.LocalizationManager.Instance.CurrentLanguage;
            FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(AvaloniaTestHelper.PinnedLanguage);

            // Alcance amplio: el finally de restauración lo consume aunque el try lanzase antes de aplicar.
            bool appliedTheme = false;

            Window? window = null;

            try
            {
                appliedTheme = ApplyCaptureTheme(theme);

                window = windowFactory()
                    ?? throw new InvalidOperationException(
                        "La fábrica de ventana devolvió 'null': no hay nada que capturar.");

                window.WindowState = WindowState.Normal;
                window.ShowInTaskbar = false;

                // La sesión headless no compone acrílico ni transparencias: sin esta normalización la
                // captura sale con píxeles transparentes, imposibles de comparar. El fondo base del tema
                // es el sustituto determinista del «blurred backdrop» real.
                window.SetRenderScaling(1.0);

                if (window.Background is not Avalonia.Media.ISolidColorBrush solid || solid.Color.A == 0)
                {
                    window.Background = ResolveBrush("AppBackgroundBrush");
                }

                window.Show();

                Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException(
                        $"No se pudo capturar el fotograma de '{window.GetType().Name}': la sesión headless " +
                        "debe inicializarse con Skia y sin 'UseHeadlessDrawing' (ver FileFlowTestAppBuilder).");

                using (frame)
                {
                    using var stream = new MemoryStream();
                    frame.Save(stream, new PngBitmapEncoderOptions
                    {
                        CompressionLevel = System.IO.Compression.CompressionLevel.Optimal
                    });

                    return stream.ToArray();
                }
            }
            finally
            {
                // Misma higiene que CaptureCore, pero también si la captura lanzó a mitad de camino.
                if (window != null)
                {
                    window.IsEnabled = false;
                    PurgeBindings(window);
                    window.Close();
                }

                if (!string.Equals(
                        FileFlow.Sdk.Localization.LocalizationManager.Instance.CurrentLanguage,
                        previousLanguage,
                        StringComparison.OrdinalIgnoreCase))
                {
                    FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(previousLanguage);
                }

                if (appliedTheme)
                {
                    RestoreCaptureTheme(previousThemeId);
                }
            }
        });
    }

    /// <summary>
    /// Igual que <see cref="Capture(Func{Control}, int, int, string)"/> pero con la altura que pida el propio
    /// contenido. Es lo que necesitan las barras: su altura depende del tema (tipografía, espaciado,
    /// decoraciones), así que fijarla a mano recortaría la captura justo cuando el diseño creciera.
    /// </summary>
    public static byte[] CaptureNaturalHeight(Func<Control> contentFactory, int width, string themeId)
        => CaptureCore(contentFactory, width, null, themeId);

    /// <summary>
    /// Lee un token del tema activo. Falla con un mensaje explícito si no está publicado o si se pregunta
    /// fuera del hilo de UI (donde <c>Application.Current</c> puede no existir todavía y el <c>null</c>
    /// resultante parecería «el token no existe»).
    /// </summary>
    public static T ResolveToken<T>(string key)
    {
        AvaloniaTestHelper.RequireUIThread($"VisualSnapshot.ResolveToken('{key}')");

        if (Application.Current is { } application &&
            application.TryFindResource(key, out var value) &&
            value is T typed)
        {
            return typed;
        }

        throw new InvalidOperationException(
            $"El token '{key}' no está publicado como {typeof(T).Name} en el tema activo: " +
            $"'{Application.Current!.GetType().Name}' no expone ese recurso con ese tipo.");
    }

    /// <summary>Definición del preset integrado con el id indicado.</summary>
    public static ThemeDefinition ResolveTheme(string themeId) =>
        BuiltInThemesCatalog.GetThemes().FirstOrDefault(t => t.Id.Equals(themeId, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException(
            $"No existe el tema integrado '{themeId}'. Disponibles: " +
            string.Join(", ", BuiltInThemesCatalog.GetThemes().Select(t => t.Id)) + ".", nameof(themeId));

    /// <summary>
    /// Aplica el tema de la captura salvo que <b>ya esté aplicado y sea semánticamente idéntico</b>.
    ///
    /// Cada captura aplicaba su tema y restauraba el anterior en el <c>finally</c>: una tanda de capturas
    /// sobre el mismo preset encadenaba construir el diccionario completo de recursos y publicar el cambio
    /// por toda la aplicación (diccionarios, variante de FluentTheme y ventanas abiertas) dos veces por
    /// captura cuando bastaba una vez por cambio real de preset. La comparación es semántica —serialización
    /// de la definición, no referencia— porque <c>ResolveTheme</c> devuelve instancias nuevas y una prueba
    /// puede haber mutado el tema activo entre capturas; en ese caso el <c>SetTheme</c> sigue ocurriendo y
    /// la captura sigue viendo el preset pedido.
    /// </summary>
    /// <returns><c>true</c> si hizo falta aplicar (y por tanto restaurar luego tendrá sentido).</returns>
    private static bool ApplyCaptureTheme(ThemeDefinition theme)
    {
        var manager = ThemeManager.Instance;
        var active = manager.ActiveThemeDefinition;

        if (active is not null &&
            string.Equals(active.Id, theme.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                System.Text.Json.JsonSerializer.Serialize(active, FileFlow.Sdk.Serialization.JsonDefaults.RelaxedOptions),
                System.Text.Json.JsonSerializer.Serialize(theme, FileFlow.Sdk.Serialization.JsonDefaults.RelaxedOptions),
                StringComparison.Ordinal))
        {
            return false;
        }

        manager.SetTheme(theme);
        return true;
    }

    /// <summary>
    /// Restaura el tema previo a la captura sólo si de verdad cambió: con el cortocircuito de
    /// <see cref="ApplyCaptureTheme"/>, la restauración tras una captura del mismo tema ya activo es un
    /// <c>SetTheme</c> completo que no corrige nada — y en una tanda de capturas sobre el mismo preset son
    /// la mitad de las aplicaciones de tema del camino.
    /// </summary>
    private static void RestoreCaptureTheme(string previousThemeId)
    {
        if (string.Equals(ThemeManager.Instance.CurrentThemeId, previousThemeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ThemeManager.Instance.SetThemeById(previousThemeId);
    }

    /// <summary>Color de un píxel de una captura PNG (para comprobaciones puntuales sobre el render).</summary>
    public static Rgba32 PixelAt(byte[] png, int x, int y)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(png);

        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"El píxel ({x},{y}) está fuera de la imagen {image.Width}x{image.Height}.");
        }

        return image[x, y];
    }

    /// <summary>
    /// Compara la captura con su línea base. Escribe la imagen y falla si no existe (para que se revise) o si
    /// difiere por encima de la tolerancia.
    /// </summary>
    public static void AssertMatchesBaseline(string name, byte[] actualPng)
    {
        Directory.CreateDirectory(BaselineDirectory);
        string baselinePath = BaselinePath(name);

        if (UpdateRequested || !File.Exists(baselinePath))
        {
            File.WriteAllBytes(baselinePath, actualPng);

            if (UpdateRequested)
            {
                return;
            }

            throw new InvalidOperationException(
                $"No existía la línea base '{name}': se acaba de crear en '{baselinePath}'. " +
                "Revisa la imagen (es el aspecto que se acaba de congelar) y vuelve a ejecutar el test; " +
                "esta primera ejecución falla a propósito para que ninguna imagen se bendiga sola.");
        }

        using var expected = SixLabors.ImageSharp.Image.Load<Rgba32>(baselinePath);
        using var actual = SixLabors.ImageSharp.Image.Load<Rgba32>(actualPng);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            WriteArtifacts(name, actualPng, File.ReadAllBytes(baselinePath), null);
            throw new InvalidOperationException(
                $"La captura '{name}' cambió de tamaño: línea base {expected.Width}x{expected.Height} frente a " +
                $"{actual.Width}x{actual.Height} de la captura actual. Artefactos en '{ArtifactDirectory}'.");
        }

        long differing = 0;
        int maxDelta = 0;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        expected.ProcessPixelRows(actual, (expectedRows, actualRows) =>
        {
            for (int y = 0; y < expectedRows.Height; y++)
            {
                var expectedRow = expectedRows.GetRowSpan(y);
                var actualRow = actualRows.GetRowSpan(y);

                for (int x = 0; x < expectedRow.Length; x++)
                {
                    int delta = Math.Max(
                        Math.Max(Math.Abs(expectedRow[x].R - actualRow[x].R), Math.Abs(expectedRow[x].G - actualRow[x].G)),
                        Math.Max(Math.Abs(expectedRow[x].B - actualRow[x].B), Math.Abs(expectedRow[x].A - actualRow[x].A)));

                    maxDelta = Math.Max(maxDelta, delta);

                    if (delta <= ChannelTolerance)
                    {
                        continue;
                    }

                    differing++;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        });

        long total = (long)expected.Width * expected.Height;
        double ratio = (double)differing / total;

        if (ratio <= AllowedDifferingPixelRatio)
        {
            return;
        }

        byte[] diff = BuildDiffImage(expected, actual);

        WriteArtifacts(name, actualPng, File.ReadAllBytes(baselinePath), diff);

        throw new InvalidOperationException(
            $"La captura '{name}' difiere de su línea base: {differing} de {total} píxeles distintos " +
            $"({ratio:P2} > {AllowedDifferingPixelRatio:P2} permitido), delta máximo por canal {maxDelta}. " +
            $"Zona afectada: x {minX}..{maxX}, y {minY}..{maxY} (ventana {expected.Width}x{expected.Height}). " +
            $"Compara 'actual.png' con '{baselinePath}' en '{ArtifactDirectory}'. Si el cambio es intencionado, " +
            "regenera con FILEFLOW_UPDATE_VISUALS=1.");
    }

    /// <summary>
    /// Cuerpo de la captura. Se marca privado y con la exigencia de hilo explícita: es el único punto que
    /// asume que ya estamos en el hilo de UI, y así el contrato queda también para quien lea el código.
    /// </summary>
    private static byte[] CaptureCore(Func<Control> contentFactory, int width, int? height, string themeId)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);

        return AvaloniaTestHelper.RunOnUI(() =>
        {
            var content = contentFactory()
                ?? throw new InvalidOperationException("La fábrica de contenido devolvió 'null': no hay nada que capturar.");

            AvaloniaTestHelper.RequireUIThread($"VisualSnapshot.Capture de {content.GetType().Name}");

            var theme = ResolveTheme(themeId);
            string previousThemeId = ThemeManager.Instance.CurrentThemeId;

            // El idioma es un estado global del proceso que otras colecciones exclusivas (Localization)
            // cambian durante sus pruebas ('en-US', 'fr-FR') y restauran sólo al terminar cada prueba: dos
            // colecciones exclusivas sí corren a la vez entre sí, así que una captura puede pillare el
            // hueco y renderizar sus textos en otro idioma. Fijar el idioma —y restaurarlo al salir—
            // cierra esa ventana temporal. Se hace en el hilo de UI y con la sesión viva, así que los
            // bindings reaccionan de forma segura.
            string previousLanguage = FileFlow.Sdk.Localization.LocalizationManager.Instance.CurrentLanguage;
            FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(AvaloniaTestHelper.PinnedLanguage);

            // Alcance amplio: el finally de restauración lo consume aunque el try lanzase antes de aplicar.
            bool appliedTheme = false;

            try
            {
                appliedTheme = ApplyCaptureTheme(theme);

                var window = new Window
                {
                    Width = width,
                    Height = height ?? 240,
                    SizeToContent = SizeToContent.Manual,
                    WindowDecorations = WindowDecorations.None,
                    CanResize = false,
                    ShowInTaskbar = false,
                    Content = content,
                    Background = ResolveBrush("AppBackgroundBrush")
                };

                window.SetRenderScaling(1.0);
                window.Show();

                // Primera pasada: el árbol se construye y los estilos se aplican, que es lo que hace que
                // 'DesiredSize' sea el del tema y no el del control desnudo.
                Dispatcher.UIThread.RunJobs();

                int capturedHeight;

                if (height is { } fixedHeight)
                {
                    capturedHeight = fixedHeight;
                }
                else
                {
                    content.Measure(new Avalonia.Size(width, double.PositiveInfinity));
                    capturedHeight = (int)Math.Ceiling(content.DesiredSize.Height);

                    window.Height = capturedHeight;
                    Dispatcher.UIThread.RunJobs();
                }

                var frame = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException(
                        $"No se pudo capturar el fotograma de '{content.GetType().Name}': la sesión headless " +
                        "debe inicializarse con Skia y sin 'UseHeadlessDrawing' (ver FileFlowTestAppBuilder).");

                using (frame)
                {
                    var expectedSize = new PixelSize(width, capturedHeight);

                    if (frame.PixelSize != expectedSize)
                    {
                        // Un escalado distinto del 1:1 haría que la línea base dependiera de la máquina.
                        throw new InvalidOperationException(
                            $"La captura de '{content.GetType().Name}' salió a {frame.PixelSize} en lugar de " +
                            $"{expectedSize}: el escalado de render no es 1:1 y la comparación no sería reproducible.");
                    }

                    using var stream = new MemoryStream();
                    frame.Save(stream, new PngBitmapEncoderOptions
                    {
                        CompressionLevel = System.IO.Compression.CompressionLevel.Optimal
                    });

                    // La captura ya es un array de bytes: el árbol de controles ha cumplido su función. Si
                    // se queda montado y alcanzable, sus bindings sobreviven al test y reaccionan a los
                    // singletons (LocalizationManager, ModelSessionRegistry, preferencias) cuando otra
                    // colección del suite paralelo está corriendo — con la colección ya finalizada, en un
                    // hilo que ya no es el del Dispatcher: 'The calling thread cannot access this object'
                    // en pruebas ajenas. Purgar bindings y desactivar la ventana corta todas esas rutas de
                    // reentrada de forma determinista, sin depender del GC ni de los finalizadores.
                    window.IsEnabled = false;
                    PurgeBindings(content);
                    window.Close();

                    return stream.ToArray();
                }
            }
            finally
            {
                if (!string.Equals(
                        FileFlow.Sdk.Localization.LocalizationManager.Instance.CurrentLanguage,
                        previousLanguage,
                        StringComparison.OrdinalIgnoreCase))
                {
                    FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(previousLanguage);
                }

                if (appliedTheme)
                {
                    RestoreCaptureTheme(previousThemeId);
                }
            }
        });
    }

    /// <summary>Lee un brush del diccionario de la aplicación ya temado (falla si no existe: sería un token muerto).</summary>
    private static IBrush ResolveBrush(string key) => ResolveToken<IBrush>(key);

    /// <summary>
    /// Desmonta un árbol de controles usado en una prueba de UI (captura o ventana de humo): anula el
    /// <c>DataContext</c> de cada control, de abajo arriba. Debe llamarse en el hilo de UI.
    /// </summary>
    public static void DetachTree(Control root) => PurgeBindings(root);

    /// <summary>
    /// Desmonta un árbol de controles usado en una captura, de abajo arriba: anula el
    /// <c>DataContext</c> y limpia <b>todas</b> las propiedades de Avalonia registradas en cada control.
    ///
    /// Los bindings de Avalonia se suscriben (débilmente) a su origen y ese origen vive más que la captura:
    /// además del <c>DataContext</c> de los view models, las vistas enlazan textos y tooltips al singleton
    /// de localización con <c>{Binding [Clave], Source={x:Static loc:LocalizationManager.Instance}}</c>.
    /// Sin esta purga, el árbol queda «vivo» tras el test: si otra colección del suite paralelo cambia la
    /// cultura o el estado de los modelos de IA en ese instante, un binding zombi intenta escribir una
    /// propiedad animable de un control propiedad del hilo del Dispatcher —la sesión ya se apagó— y la
    /// prueba que falla es otra, con un 'The calling thread cannot access this object' que no menciona la
    /// captura.
    ///
    /// Anular el <c>DataContext</c> corta los bindings que cuelgan del view model; el barrido de
    /// <c>ClearValue</c> elimina además los bindings de prioridad local (los declarados en el markup del
    /// control contra la fuente estática, que el DataContext no alcanza). Avalonia no tiene el
    /// <c>BindingOperations.ClearAllBindings</c> de WPF: este barrido es su equivalente. Se ejecuta en el
    /// hilo de UI, así que las reevaluaciones de los bindings al desprenderse son seguras.
    /// </summary>
    private static void PurgeBindings(Control root)
    {
        // La unión de árbol visual y árbol lógica no es un lujo: un TabControl materializa sólo la
        // pestaña seleccionada (los controles de las otras viven en el árbol LÓGICO, no en el visual),
        // y un ContentPresenter no instanciado también deja rama lógica sin rama visual. Purgar sólo
        // lo visual dejaba bindings zombis en pestañas no seleccionadas: al notificar el singleton de
        // localización desde otro hilo, el binding escribía una propiedad de un control propiedad del
        // hilo de UI ya apagado — el clásico 'The calling thread cannot access this object' en pruebas
        // ajenas. Ambos árboles se recorren y se deduplican: se solapan y el mismo control aparecería
        // dos veces.
        var controls = new HashSet<Control>();

        foreach (var visual in root.GetSelfAndVisualDescendants().OfType<Control>())
        {
            controls.Add(visual);
        }

        foreach (var logical in root.GetLogicalDescendants().OfType<Control>())
        {
            controls.Add(logical);
        }

        // De abajo arriba: da igual para el resultado (todo va a quedar limpio), pero así las
        // reevaluaciones intermedias de los contenedores no se propagan a hijos ya desmontados.
        var ordered = controls.ToList();
        ordered.Reverse();

        foreach (var control in ordered)
        {
            control.DataContext = null;

            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(control))
            {
                control.ClearValue(property);
            }
        }
    }

    /// <summary>
    /// Mapa de diferencias legible: la captura actual con lo que NO cambió atenuado, de forma que lo que se
    /// movió resalta a simple vista al revisar el fallo.
    /// </summary>
    private static byte[] BuildDiffImage(Image<Rgba32> expected, Image<Rgba32> actual)
    {
        using var diff = actual.Clone();

        expected.ProcessPixelRows(diff, (expectedRows, diffRows) =>
        {
            for (int y = 0; y < expectedRows.Height; y++)
            {
                var expectedRow = expectedRows.GetRowSpan(y);
                var diffRow = diffRows.GetRowSpan(y);

                for (int x = 0; x < diffRow.Length; x++)
                {
                    if (expectedRow[x] == diffRow[x])
                    {
                        var pixel = diffRow[x];
                        diffRow[x] = new Rgba32((byte)(pixel.R / 4), (byte)(pixel.G / 4), (byte)(pixel.B / 4), pixel.A);
                    }
                }
            }
        });

        using var stream = new MemoryStream();
        diff.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static void WriteArtifacts(string name, byte[] actualPng, byte[]? expectedPng, byte[]? diffPng)
    {
        Directory.CreateDirectory(ArtifactDirectory);

        File.WriteAllBytes(Path.Combine(ArtifactDirectory, $"{name}-actual.png"), actualPng);

        if (expectedPng != null)
        {
            File.WriteAllBytes(Path.Combine(ArtifactDirectory, $"{name}-expected.png"), expectedPng);
        }

        if (diffPng != null)
        {
            File.WriteAllBytes(Path.Combine(ArtifactDirectory, $"{name}-diff.png"), diffPng);
        }
    }
}
