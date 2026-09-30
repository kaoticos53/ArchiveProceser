using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de la sección de TELEMETRÍA de la ficha del host Uno (hito 275) y de su MONTAJE.
///
/// <para><b>El defecto que vigila</b>. La ficha construía las filas de telemetría, las rellenaba desde
/// <c>CurrentStats</c> y <b>no las montaba en ninguna parte</b>: la fuente de las medidas era cierta, la
/// construcción también, y el usuario no veía ni una medida. La guardia que existía medía justo eso —que las
/// filas se construyeran—, así que pasaba en verde con la sección invisible. Lo que se vigila aquí es la
/// <b>oferta</b>: que la sección sea una superficie declarada de la ficha, que el panel la monte, que le ate el
/// nodo y que la sonda lo recorra entero (pestaña, montaje, conmutación y filas pintadas).</para>
///
/// <para><b>Por qué la sección es de SÓLO LECTURA</b>. La fuente (<c>CurrentStats</c>, el agregado que escribe el
/// motor, y el estado del nodo) ya existe y la sección no la escribe nadie. El «Vaciar métricas» que la ficha
/// construía no tiene gemelo en la versión anterior —su pestaña de telemetría no ofrece borrar nada— y ninguna vista
/// del producto lo monta: dibujarlo aquí sería una capacidad NUEVA de este host, no paridad. Por eso hay una
/// aserción <b>negativa</b>: si alguien vuelve a cablear la escritura, el caso cae nombrando la frontera.</para>
///
/// <para><b>Por qué lee la fuente y no materializa la vista</b>: el panel es WinUI y no se materializa en la
/// sesión de pruebas (la lección del lienzo, hito 232). El riesgo real es la deriva: que la sección se quede sin
/// montar, que se alimente de un contador propio del host o que la pestaña pierda su ancla.</para>
/// </summary>
public class UnoInspectorTelemetryGuardTests
{
    private const string SectionPath = "FileFlow.App.Uno/Controls/NodeInspectorTelemetrySection.cs";
    private const string PanelPath = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";

    private static string Section() => SourceText.CodeWithoutComments(SectionPath);

    /// <summary>La superficie de observación de la ficha, en su archivo propio (la otra mitad del panel).</summary>
    private static string Probes() => SourceText.CodeWithoutComments(
        "FileFlow.App.Uno/Controls/NodeInspectorPanel.Probes.cs");

    private static string Panel() => SourceText.CodeWithoutComments(PanelPath);

    [Fact]
    public void TheTelemetrySection_ShouldOnlyReadTheMetricsTheEngineWrites()
    {
        string section = Section();

        section.Should().Contain(
            "node.CurrentStats",
            "las medidas salen del NodeViewModel (el agregado que escribe el MOTOR), no de un contador propio del host");

        section.Should().Contain(
            "node.ExecutionStatusText",
            "el estado del nodo es la propiedad localizada del view model portable");

        section.Should().Contain(
            "nameof(NodeViewModel.CurrentStats) or nameof(NodeViewModel.ExecutionStatus)",
            "la sección sigue al nodo por PropertyChanged: las medidas cambian mientras la ficha está abierta");

        // Las filas se materializan UNA vez: el latido del motor refresca la sección mientras hay ejecución, y
        // volver a construirla en cada fotograma crearía y tiraría quince elementos por latido sin cambiar nada
        // de lo que se ve (la versión anterior, con enlaces, tampoco las reconstruye).
        section.Should().Contain(
            "private void EnsureRows()",
            "la fila se materializa aparte del refresco: refrescar es reescribir su texto");

        section.Should().Contain(
            "if (_builtRows.Count > 0)",
            "y materializarla es idempotente: el refresco no puede volver a construir lo que ya está");

        // Las cinco medidas, con su clave del diccionario y su texto de reserva (los mismos rótulos que la ficha
        // construía antes de montarse): la sección no inventa textos ni medidas.
        foreach (string measure in new[]
                 {
                     "\"Uno_InspectorStatus\", \"Estado\"",
                     "\"Uno_InspectorProcessed\", \"Procesados\"",
                     "\"Uno_InspectorAvgLatency\", \"Latencia media\"",
                     "\"Uno_InspectorTotalTime\", \"Tiempo total\"",
                     "\"Uno_InspectorPeakRam\", \"Pico de memoria\""
                 })
        {
            section.Should().Contain(measure,
                $"la medida '{measure}' se rotula por el diccionario del host, como el resto de la ficha");
        }

        // Y cada valor canta su ancla: el contenedor de la fila no materializa en el árbol de accesibilidad, así
        // que sin ancla en el valor la observación externa no puede leer ni una medida.
        foreach (string anchor in new[]
                 {
                     "InspectorTelemetry_Status",
                     "InspectorTelemetry_Processed",
                     "InspectorTelemetry_AvgLatency",
                     "InspectorTelemetry_TotalTime",
                     "InspectorTelemetry_PeakRam"
                 })
        {
            section.Should().Contain(anchor,
                $"el valor de la medida canta '{anchor}': es lo que la observación UIA externa lee");
        }

        section.Should().Contain(
            "if (_node is null)",
            "sin nodo inspeccionado no se pinta ni una medida: la sección se queda sin filas en vez de mentir con ceros de nadie");

        // La frontera, escrita como aserción: la sección SOLO lee. El vaciado que la ficha construía y nunca
        // montó no se repone aquí —la versión anterior no lo ofrece y ninguna vista del producto lo dibuja—, así que
        // la sección no puede citar la escritura de las estadísticas ni el comando de reinicio del núcleo.
        section.Should().NotContain(
            "UpdateTelemetryStats",
            "la sección no escribe las medidas: el host no inventa un vaciado que la versión anterior no ofrece");

        section.Should().NotContain(
            "ResetNodeMetrics",
            "y tampoco enruta la escritura por el comando del view model: montarlo sería capacidad nueva de ESTE host");
    }

    [Fact]
    public void TheInspector_ShouldMountTheTelemetrySection_InItsOwnDeclaredSection()
    {
        string panel = Panel();

        panel.Should().Contain(
            "(\"Uno_InspectorTelemetry\", \"Telemetría\", \"InspectorTabTelemetry\")",
            "la Telemetría es una SECCIÓN declarada de la ficha, con su ancla, como las otras cinco: su rótulo es el de la pestaña");

        panel.Should().Contain(
            "NamedPane(\"InspectorTelemetryScroll\", _telemetrySection)",
            "y el panel la MONTA con la sección por contenido: es el defecto del hito 275 (construida y sin montar)");

        panel.Should().Contain(
            "_telemetrySection.Bind(_inspected)",
            "el nodo se le da por el mismo camino que a las otras secciones (el nodo inspeccionado del VM)");

        panel.Should().Contain(
            "_telemetrySection.Bind(null)",
            "y se le quita al quedarse la ficha sin nodo, sin dejar las medidas del anterior");

        panel.Should().Contain(
            "_telemetrySection.ApplyLocalization()",
            "el cambio de idioma en caliente alcanza también a sus rótulos");

        // Los fantasmas: el bloque que se rellenaba para nadie no puede volver en ninguna de sus formas.
        foreach (string phantom in new[] { "_telemetryRows", "_telemetryHeader", "_resetMetricsButton", "RebuildTelemetry" })
        {
            panel.Should().NotContain(phantom,
                $"'{phantom}' era la superficie que se construía y no se montaba: si vuelve, vuelve el defecto");
        }

        // La medición en runtime: la sonda recorre la cadena entera y una fila en blanco no la aprueba. La
        // sonda de los paneles vive en su propio archivo (el orquestador del sondeo no la alberga desde el
        // reorden del hito 276): ésa es la casa de las líneas `insp.*` que este caso cita.
        string selfcheck = SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckPanels.cs");

        selfcheck.Should().Contain(
            "insp.ProbeTelemetrySection()",
            "el selfcheck corre la sonda de la sección: sin ella, volver a dejarla sin montar pasaría desapercibido");

        selfcheck.Should().Contain(
            "telemetry.Ok",
            "y afirma el resultado con sus filas y sus filas en blanco");

        // Y mide el ida y vuelta al nodo: sin nodo la sección se queda sin filas, y al reatarlo vuelve a
        // materializarlas. Es la mitad que el puntero no puede recorrer (deseleccionar en el lienzo no vacía la
        // ficha), así que la cubre la sonda en proceso — y esa medida vive en la superficie de observación del
        // panel, no en el fichero que lo construye.
        Probes().Should().Contain(
            "withoutNode == 0",
            "la sonda mide que sin nodo no queda ni una fila y que al reatarlo se vuelven a materializar las suyas");
    }
}
