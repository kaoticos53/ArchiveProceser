using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// <b>Los ejemplos del catálogo, ejecutados de punta a punta: cada uno tiene que entregar lo que promete.</b>
///
/// <para><c>WorkflowExamplesValidationTests</c> juzga el <b>papel</b> del ejemplo —que sea del formato del
/// producto, que sus puertos existan, que se abra entero en el editor—. Esto juzga el <b>trabajo</b>: se siembra
/// un área de trabajo, se ejecuta el flujo de verdad sobre el disco y se mira qué quedó. Un ejemplo puede pasar
/// las pruebas de papel y no entregar nada: el 21 acumula un lote que nunca se llena y termina en verde sin una
/// sola copia; el 26 escribía fuera del área de trabajo; el 15 filtraba por una propiedad (<c>WordCount</c>) que
/// ningún nodo publica; el 22 y el 30 declaraban una barrera de sincronización que el motor rechazaba por
/// ciclo; el 38 dejaba archivos de <b>cero bytes</b> con la extensión del archivo prometido. Los cinco se
/// destaparon aquí (hito 204) y ninguno se veía desde fuera: el flujo terminaba en verde. El hilo siguió en el
/// hito 205: el 21 y el 34 entregaban <b>cero</b> con la entrada sembrada —el lote no se llenaba nunca y lo que
/// quedaba dentro moría con la ejecución, y además alimentaban el compresor con el <i>marcador</i> del lote, un
/// elemento sintético sin ruta, en vez de con los elementos del lote—.</para>
///
/// <para>La tabla de asientos es el contrato: <b>un asiento por cada archivo del catálogo</b>, con la promesa
/// que se le puede exigir de verdad. Las que no se pueden comprobar sin depender de la máquina —un vídeo real,
/// un webhook, la papelera del usuario— se declaran con su motivo en vez de fingir una comprobación, y la
/// guardia falla si un ejemplo nuevo entra sin asiento. Un ejemplo sin asiento es un ejemplo del que nadie sabe
/// si entrega.</para>
///
/// <para>Hay tres exigencias que <b>no dependen del asiento</b>, porque valen para cualquier ejemplo: el motor
/// tiene que <b>aceptar</b> el grafo (un ejemplo rechazado no es un ejemplo), la ejecución no puede reventar, y
/// no se puede escribir fuera del área de trabajo ni dejar un archivo de cero bytes. Un asiento
/// <see cref="Promise.Declared"/> renuncia a juzgar lo que el flujo entrega —y escribe por qué—, nunca a esas
/// tres.</para>
///
/// <para><b>Y una cuarta, sobre la entrada:</b> lo sembrado en <c>Input/</c> sale de ahí <i>igual</i> que como
/// entró, salvo que el asiento declare otra cosa (<see cref="InputPolicy"/>). El banco miraba lo entregado y
/// excluía los archivos de entrada de la cuenta, así que quien los tocara no lo delataba nadie: el ejemplo 21
/// devolvía el <c>paquete.zip</c> sembrado con <b>22 bytes</b> donde tenía 148 —el compresor abría su propio
/// archivo de entrada para escribir el destino encima— y el banco lo daba por bueno (destapado en el hito 205,
/// arreglado en el nodo). Un flujo de archivos que destruye lo que le dan no es un flujo: los que de verdad lo
/// hacen —la papelera de reciclaje, la cuarentena, el renombrado en el sitio— lo dicen en su asiento.</para>
///
/// <para><b>Único efecto fuera de la jaula:</b> los ejemplos 10 y 33 mandan sus originales a la papelera de
/// reciclaje real del usuario (es su promesa, y no hay costura para fingirla sin cambiar el camino de la app).
/// Son seis archivos de texto de unos pocos bytes por vuelta de la suite.</para>
///
/// <para><b>Dónde corre cada ejemplo, y por qué es una sala limpia:</b> mientras corren los cuarenta flujos, el
/// banco apunta el <i>directorio de trabajo del proceso</i> a una carpeta temporal suya —vacía y sólo suya— y
/// exige que quede vacía: un archivo ahí es la firma de un flujo que resolvió su destino contra «la carpeta donde
/// corre». Antes medía el directorio de trabajo del suite entero (el de los binarios) con una foto antes y después
/// de cada flujo, y cualquiera que escribiera ahí durante esa ventana —una compilación en marcha en el mismo
/// árbol, un vecino de otra colección— aparecía como si lo hubiera escrito el ejemplo medido: la clase de rojo que
/// nadie puede reproducir. La sala limpia da la misma exigencia con atribución exacta, porque nadie más escribe
/// en ella. Cambiar el directorio de trabajo es estado global del proceso, así que la clase corre en su
/// <b>colección exclusiva</b> (<see cref="ExampleFlowBankCollection"/>).</para>
/// </summary>
[Collection(ExampleFlowBankCollection.Name)]
public class ExampleFlowsEndToEndTests
{
    private readonly ITestOutputHelper _output;

    public ExampleFlowsEndToEndTests(ITestOutputHelper output) => _output = output;

    /// <summary>Lo que un ejemplo promete, en términos que se pueden comprobar sobre el disco.</summary>
    private enum Promise
    {
        /// <summary>Cada archivo de la entrada aparece en alguna carpeta de destino (el nombre puede cambiar).</summary>
        EveryInputDelivered,

        /// <summary>Se entrega al menos un archivo de este tipo (por ejemplo un <c>.webp</c> optimizado).</summary>
        DeliversAKind,

        /// <summary>El comprimido de la entrada se descomprime y su contenido aparece en el disco.</summary>
        UnpacksTheArchive,

        /// <summary>De los dos archivos idénticos uno se entrega y el otro acaba en cuarentena.</summary>
        QuarantinesTheDuplicate,

        /// <summary>Los originales salen de la entrada (papelera de reciclaje).</summary>
        EmptiesTheInput,

        /// <summary>Carpetas vacías de la entrada eliminadas.</summary>
        CleansEmptyFolders,

        /// <summary>El flujo no entrega archivos: inspecciona, registra, notifica o sincroniza ramas.</summary>
        NothingToDisk,

        /// <summary>
        /// Promesa declarada como <b>no comprobable aquí</b>, con el motivo. No se finge una comprobación: se
        /// deja escrito por qué, y la única exigencia es la de siempre (no escribe fuera de su área de trabajo).
        /// </summary>
        Declared
    }

    /// <summary>
    /// Qué puede pasarle a lo sembrado en <c>Input/</c> mientras el flujo corre. El caso corriente es que no le
    /// pase nada —el pipeline lee, copia, comprime, analiza—, y ése es el que se exige por omisión: <b>un flujo no
    /// puede tocar la entrada sin que su asiento lo diga</b>.
    /// </summary>
    private enum InputPolicy
    {
        /// <summary>Nada: cada archivo de entrada sigue en su ruta, con los mismos bytes.</summary>
        Untouched,

        /// <summary>
        /// El flujo puede llevárselos de su ruta —renombrar en el sitio, mover a cuarentena—, pero no perder lo
        /// que traían: los bytes de cada entrada tienen que seguir en alguna parte del área de trabajo.
        /// </summary>
        MovedButKept,

        /// <summary>
        /// La promesa del flujo es destruir la entrada (la papelera de reciclaje). Los bytes pueden desaparecer, y
        /// lo que quede lo juzga su asiento.
        /// </summary>
        MayBeDestroyed
    }

    /// <summary>
    /// Un asiento: qué promete el ejemplo, y qué se le permite tocar. <paramref name="InputWhy"/> es la excusa
    /// escrita para los que dejan tocar la entrada, y sobra —la guardia lo exige vacío— en los que no.
    /// </summary>
    private sealed record Seat(
        string Flow,
        Promise Promise,
        string Detail,
        InputPolicy Inputs = InputPolicy.Untouched,
        string InputWhy = "");

    /// <summary>
    /// El contrato: 40 ejemplos, un asiento cada uno. `Detail` es la promesa en palabras (para el tipo), el
    /// motivo por el que no se puede comprobar (para <see cref="Promise.Declared"/>) o la extensión esperada
    /// (para <see cref="Promise.DeliversAKind"/>).
    /// </summary>
    private static readonly Seat[] Seats =
    [
        new("flow_01_organizador_imagenes", Promise.DeliversAKind, ".webp"),
        new("flow_02_conversion_media_mp3", Promise.Declared,
            "pide un vídeo de entrada para prometer audio; con entradas que no son vídeo entrega copias con extensión .mp3 (defecto del hito 204, sin arreglar)"),
        new("flow_03_calculo_hashes_sha256", Promise.EveryInputDelivered, ""),
        new("flow_04_descompresion_directa", Promise.UnpacksTheArchive, "dentro.txt"),
        new("flow_05_renombrado_basico_fechas", Promise.EveryInputDelivered, ""),
        new("flow_06_limpieza_carpetas_vacias", Promise.CleansEmptyFolders, ""),
        new("flow_07_inyeccion_variables_metadatos", Promise.EveryInputDelivered, ""),
        new("flow_08_compresion_zip_automatica", Promise.DeliversAKind, ".zip"),
        new("flow_09_registro_consola_log", Promise.EveryInputDelivered, ""),
        new("flow_10_papelera_reciclaje_segura", Promise.EmptiesTheInput,
            "los originales se van a la papelera de reciclaje REAL del usuario, así que la entrada queda vacía: es la única prueba que recorre de verdad el contrato del struct de shell (el que tiraba el proceso con 0xC0000005 antes del hito 204)",
            InputPolicy.MayBeDestroyed,
            "la promesa del ejemplo es precisamente vaciar la entrada: los seis archivos acaban en la papelera del sistema, fuera del área de trabajo, y por eso sus bytes no se pueden buscar en ella"),
        new("flow_11_clasificacion_por_extension", Promise.DeliversAKind, ".mp4"),
        new("flow_12_filtro_tamano_archivos", Promise.EveryInputDelivered, ""),
        new("flow_13_deduplicacion_por_hash", Promise.QuarantinesTheDuplicate, "",
            InputPolicy.MovedButKept,
            "el duplicado se mueve a la cuarentena del área de trabajo: en su ruta ya no está —de los dos idénticos sólo queda uno— y sus bytes siguen enteros"),
        new("flow_14_extraccion_exif_fotografia", Promise.EveryInputDelivered, ""),
        new("flow_15_inspeccion_documentos_texto", Promise.DeliversAKind, ".csv"),
        new("flow_16_notificacion_webhook_discord", Promise.Declared,
            "notifica a un webhook de Discord: sin un servicio al que apuntar no hay entrega que comprobar"),
        new("flow_17_accion_cuarentena_originales", Promise.QuarantinesTheDuplicate, "",
            InputPolicy.MovedButKept,
            "la acción del flujo es mover los originales a cuarentena: salen de su ruta y siguen enteros dentro del área de trabajo"),
        new("flow_18_inspeccion_tipo_directorio", Promise.NothingToDisk, "solo inspecciona carpetas"),
        new("flow_19_ejecucion_script_cli", Promise.NothingToDisk, "ejecuta un comando y registra"),
        new("flow_20_filtro_archivos_rar_volumenes", Promise.UnpacksTheArchive, "dentro.txt"),
        // El lote es de 10 y la entrada sembrada trae seis archivos: durante la ejecución no se cierra ninguno, así
        // que lo que llegue al destino sólo puede salir del cierre de la ejecución (hito 205). Con el lote cerrado,
        // sus elementos van por ItemOut al compresor.
        new("flow_21_procesamiento_por_lotes_batch", Promise.DeliversAKind, ".zip"),
        new("flow_22_paralelismo_fork_join", Promise.EveryInputDelivered, ""),
        new("flow_23_regulacion_velocidad_rate_limit", Promise.EveryInputDelivered, ""),
        new("flow_24_transcodificacion_multiformato", Promise.Declared,
            "promete vídeo y GIF a partir de un vídeo; con entradas que no son vídeo entrega copias con la extensión de destino (defecto del hito 204, sin arreglar)"),
        new("flow_25_pipeline_seguridad_analisis", Promise.DeliversAKind, ".png"),
        new("flow_26_organizacion_cronologica_exif", Promise.DeliversAKind, "Imagen",
            InputPolicy.MovedButKept,
            "el flujo organiza los archivos por fecha moviéndolos de carpeta dentro del área de trabajo: cambian de ruta, no de bytes (su asiento ya exige que estén entregados)"),
        new("flow_27_reintentos_y_gestion_fallos", Promise.Declared,
            "canaliza fallos hacia un webhook: sin servicio no hay entrega que comprobar"),
        new("flow_28_limpieza_y_descompresion_recursiva", Promise.UnpacksTheArchive, "dentro.txt"),
        new("flow_29_inyeccion_variables_sistema", Promise.EveryInputDelivered, ""),
        new("flow_30_notificacion_multi_canal", Promise.Declared,
            "la barrera sólo se cierra si el webhook responde, así que lo que llegue al sumidero depende de un servicio externo; lo que sí se exige —y es lo que destapó el defecto del ciclo— es que el motor acepte el grafo"),
        new("flow_31_scatter_gather_archivos", Promise.EveryInputDelivered, ""),
        new("flow_32_cadena_custodia_auditoria", Promise.Declared,
            "cadena de custodia sobre media más webhook de auditoría: ni el vídeo de entrada ni el servicio de destino están garantizados"),
        new("flow_33_optimizacion_masiva_media_multicapa", Promise.Declared,
            "depura con la papelera de reciclaje real y transcodifica media: mismo motivo que el 10 y el 24"),
        // Mismo caso que el 21 con lote de 50 y tras el renombrado corporativo: lo entregado viene del cierre de la
        // ejecución, no de un lote que se llene.
        new("flow_34_sistema_ingesta_documental_empresa", Promise.DeliversAKind, ".zip",
            InputPolicy.MovedButKept,
            "el renombrado corporativo ocurre en disco (`RenameMode: DirectInPlace`): los archivos cambian de nombre en su carpeta y conservan sus bytes (sin esa declaración el compresor de aguas abajo no tendría rutas reales que comprimir)"),
        new("flow_35_limpieza_y_mantenimiento_servidores", Promise.Declared,
            "mantenimiento de servidores: borra temporales por antigüedad y notifica; la promesa depende de las fechas del entorno"),
        new("flow_36_pipeline_etl_archivos_mixtos", Promise.Declared,
            "ETL con transcodificación de media: con entradas que no son media entrega copias con extensión de destino (defecto del hito 204, sin arreglar)"),
        new("flow_37_orquestacion_cli_con_salida_json", Promise.Declared,
            "orquesta un CLI externo y evalúa su salida JSON: el comando del ejemplo es de la máquina del autor"),
        new("flow_38_deduplicacion_y_archivado_frisado", Promise.DeliversAKind, ".7z",
            InputPolicy.MovedButKept,
            "el flujo deduplica y archiva: el original descartado sale de su ruta y el archivado conserva sus bytes dentro del área de trabajo"),
        new("flow_39_flujo_resiliente_con_fallback", Promise.Declared,
            "fallback de conversión de vídeo: necesita media de entrada real"),
        new("flow_40_centro_procesamiento_multimedia_enterprise", Promise.Declared,
            "centro multimedia con regulación de tasa, transcodificación y webhook: depende de media real y de un servicio")
    ];

    [Fact]
    public void EveryExample_ShouldHaveASeat()
    {
        var seats = Seats.Select(s => s.Flow).ToList();
        var examples = ExampleFiles().Select(Path.GetFileNameWithoutExtension).ToList();

        examples.Should().NotBeEmpty("el catálogo de ejemplos es lo que se juzga");
        examples.Should().BeSubsetOf(seats,
            "un ejemplo sin asiento es un ejemplo del que nadie sabe si entrega: añade su fila a la tabla de asientos");
        seats.Should().BeSubsetOf(examples, "un asiento de un ejemplo que ya no existe miente sobre la cobertura");
        seats.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Un asiento que juzga algo —o que renuncia a juzgarlo— tiene que decir <b>qué</b> o <b>por qué</b>. Vacío no
    /// vale: es la diferencia entre una promesa comprobada y un hueco con la forma de una promesa.
    /// </summary>
    [Fact]
    public void EveryJudgingSeat_ShouldSayWhatItExpects()
    {
        var mute = Seats
            .Where(s => string.IsNullOrWhiteSpace(s.Detail))
            .Where(s => s.Promise is Promise.Declared or Promise.DeliversAKind or Promise.UnpacksTheArchive)
            .Select(s => s.Flow)
            .ToList();

        mute.Should().BeEmpty(
            "un asiento Declared tiene que escribir por qué no se comprueba, y uno que espera algo concreto (una " +
            "extensión, un miembro del comprimido) tiene que escribirlo: sin eso el asiento no dice nada");
    }

    /// <summary>
    /// Un asiento que deja al flujo tocar la entrada tiene que escribir <b>por qué</b>, y uno que no lo deja no
    /// puede llevar la excusa puesta: la excusa de más es una promesa que ya no se cumple —el día que el ejemplo
    /// deje de destruir la entrada, su asiento seguirá diciendo que puede—.
    /// </summary>
    [Fact]
    public void EverySeatThatLetsTheFlowTouchTheInput_ShouldSayWhy()
    {
        var unjustified = Seats
            .Where(s => s.Inputs != InputPolicy.Untouched && string.IsNullOrWhiteSpace(s.InputWhy))
            .Select(s => s.Flow)
            .ToList();

        unjustified.Should().BeEmpty(
            "un asiento que permite tocar la entrada tiene que escribir por qué (InputWhy): sin eso, la excepción " +
            "no se distingue de un hueco");

        var staleExcuses = Seats
            .Where(s => s.Inputs == InputPolicy.Untouched && !string.IsNullOrWhiteSpace(s.InputWhy))
            .Select(s => s.Flow)
            .ToList();

        staleExcuses.Should().BeEmpty(
            "un asiento que exige la entrada intacta no lleva excusa: dejarla puesta es decir que el flujo toca algo " +
            "que ya no toca");
    }

    [Fact]
    public async Task EveryExample_ShouldDeliverWhatItPromises()
    {
        var problems = new List<string>();
        var report = new StringBuilder();

        // La sala limpia: el directorio de trabajo del proceso se apunta aquí mientras corren los ejemplos, para que
        // un flujo que resuelva su destino contra «donde corre» deje el archivo donde sólo el banco mira. Se
        // restaura pase lo que pase —el directorio de trabajo es del proceso entero, no de esta prueba—.
        string originalWorkingDirectory = Directory.GetCurrentDirectory();
        string room = Path.Combine(Path.GetTempPath(), $"FF_ExampleBank_{Guid.NewGuid().ToString("N")[..6]}");
        Directory.CreateDirectory(room);

        try
        {
            Directory.SetCurrentDirectory(room);

            foreach (Seat seat in Seats)
            {
                var run = await RunAsync(seat, room);
                report.AppendLine($"### {seat.Flow} [{seat.Promise}] -> {run.Outcome} (entregados {run.Produced.Count})");
                report.AppendLine($"    {seat.Detail}");
                report.AppendLine(run.TouchedInputs.Count == 0
                    ? "    entrada: intacta"
                    : $"    entrada: {string.Join(" | ", run.TouchedInputs)}");

                // El motor tiene que ACEPTAR el ejemplo. Un grafo rechazado no se ejecuta y no entrega nada: es el
                // defecto más silencioso de todos, porque el flujo llega al catálogo, se abre en el editor y nunca
                // corre. Vale para cualquier asiento, incluido el que renuncia a juzgar lo entregado.
                if (run.RefusedByEngine is not null)
                {
                    problems.Add($"[{seat.Flow}] el motor rechaza el grafo: {run.RefusedByEngine}");
                    continue;
                }

                if (run.Error is not null)
                {
                    problems.Add($"[{seat.Flow}] no llegó a ejecutarse: {run.Error}");
                    continue;
                }

                problems.AddRange(run.Strayed.Select(path =>
                    $"[{seat.Flow}] escribió en la carpeta donde corre: '{path}' (el flujo tiene que decidir su " +
                    "destino dentro de lo que se le dio, no en la carpeta donde se ejecuta)"));
                problems.AddRange(run.EmptyArtifacts.Select(path =>
                    $"[{seat.Flow}] entregó '{path}' con cero bytes: un archivo vacío con la extensión de destino " +
                    "aparenta una conversión que no ocurrió"));

                problems.AddRange(Check(seat, run));
                problems.AddRange(CheckInputs(seat, run));
            }

            _output.WriteLine(report.ToString());

            problems.Should().BeEmpty(string.Join("\n", problems));

            FilesIn(room).Should().BeEmpty(
                "nadie más escribe en la sala limpia del banco: lo que quede ahí lo dejó un ejemplo que decidió su " +
                "destino en la carpeta donde corre en vez de en lo que se le dio");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalWorkingDirectory);
            try { Directory.Delete(room, true); } catch { /* mejor esfuerzo: la carpeta es temporal */ }
        }
    }

    /// <summary>La promesa de cada asiento, comprobada sobre lo que quedó en el disco.</summary>
    private static IEnumerable<string> Check(Seat seat, Run run)
    {
        switch (seat.Promise)
        {
            case Promise.EveryInputDelivered:
                foreach (string input in run.Inputs)
                {
                    string stem = Path.GetFileNameWithoutExtension(input);
                    if (!run.Produced.Any(p => Path.GetFileNameWithoutExtension(p).Contains(stem, StringComparison.OrdinalIgnoreCase)))
                    {
                        yield return $"[{seat.Flow}] no entregó '{Path.GetFileName(input)}'";
                    }
                }
                break;

            case Promise.DeliversAKind:
                if (seat.Detail.StartsWith('.'))
                {
                    if (!run.Produced.Any(p => p.EndsWith(seat.Detail, StringComparison.OrdinalIgnoreCase)))
                    {
                        yield return $"[{seat.Flow}] no entregó ningún '{seat.Detail}'";
                    }
                }
                else if (!run.Produced.Any(p => p.Contains(seat.Detail, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return $"[{seat.Flow}] no entregó nada que contenga '{seat.Detail}'";
                }
                break;

            case Promise.UnpacksTheArchive:
                if (!run.Produced.Any(p => Path.GetFileName(p).Equals(seat.Detail, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return $"[{seat.Flow}] no extrajo '{seat.Detail}' del comprimido de la entrada";
                }
                break;

            case Promise.QuarantinesTheDuplicate:
                if (run.Produced.Count(p => p.Contains("/Quarantine/", StringComparison.OrdinalIgnoreCase)) == 0)
                {
                    yield return $"[{seat.Flow}] no puso nada en cuarentena con dos archivos idénticos en la entrada";
                }
                break;

            case Promise.EmptiesTheInput:
                if (run.Inputs.Any(File.Exists))
                {
                    yield return $"[{seat.Flow}] dejó archivos en la entrada: la promesa es vaciarla";
                }
                break;

            case Promise.CleansEmptyFolders:
                if (run.EmptyFoldersLeft.Count > 0)
                {
                    yield return $"[{seat.Flow}] no eliminó las carpetas vacías: {string.Join(", ", run.EmptyFoldersLeft)}";
                }
                break;

            case Promise.NothingToDisk:
            case Promise.Declared:
            default:
                break;
        }
    }

    /// <summary>
    /// Lo que el flujo le hizo a la entrada, juzgado por lo que su asiento permite. Vale para <b>todos</b> los
    /// asientos, incluidos los declarados: renunciar a juzgar lo que el flujo entrega no es renunciar a que deje
    /// la entrada en paz (o a que, si se la lleva, no pierda lo que traía).
    /// </summary>
    private static IEnumerable<string> CheckInputs(Seat seat, Run run)
    {
        switch (seat.Inputs)
        {
            case InputPolicy.Untouched:
                foreach (string touched in run.TouchedInputs)
                {
                    yield return $"[{seat.Flow}] tocó un archivo de entrada: {touched}. Un flujo que lee, copia o " +
                        "transforma no puede modificar ni llevarse lo que se le dio; si de verdad tiene que hacerlo, su " +
                        "asiento lo declara (`Inputs`/`InputWhy`)";
                }
                break;

            case InputPolicy.MovedButKept:
                foreach (string lost in run.LostInputBytes)
                {
                    yield return $"[{seat.Flow}] perdió los bytes de un archivo de entrada: {lost}. Su asiento permite " +
                        "moverlo, no destruirlo: lo que la entrada traía tiene que seguir en el área de trabajo";
                }
                break;

            case InputPolicy.MayBeDestroyed:
            default:
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El banco: área de trabajo, ejecución y medición
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Un archivo sembrado en <c>Input/</c>, con los bytes que traía antes de ejecutar.</summary>
    private sealed record SeededInput(string RelativePath, string Hash);

    private sealed record Run(
        string Root,
        string Outcome,
        string? Error,
        string? RefusedByEngine,
        IReadOnlyList<string> Inputs,
        IReadOnlyList<string> Produced,
        IReadOnlyList<string> Strayed,
        IReadOnlyList<string> EmptyArtifacts,
        IReadOnlyList<string> EmptyFoldersLeft,
        IReadOnlyList<string> TouchedInputs,
        IReadOnlyList<string> LostInputBytes);

    private async Task<Run> RunAsync(Seat seat, string room)
    {
        string root = Path.Combine(Path.GetTempPath(), $"FF_Example_{seat.Flow}_{Guid.NewGuid().ToString("N")[..6]}");
        Directory.CreateDirectory(root);
        SeedWorkspace(root);

        string[] inputs = [.. Directory.EnumerateFiles(Path.Combine(root, "Input"), "*", SearchOption.AllDirectories)];
        var seededInputs = inputs
            .Select(path => new SeededInput(Path.GetRelativePath(root, path).Replace('\\', '/'), HashOf(path)))
            .ToList();
        var roomBefore = FilesIn(room);

        var graph = new WorkflowStorageService().DeserializeGraph(File.ReadAllText(ExamplePath(seat.Flow)));
        var loader = PluginRegistryHelper.CreateConfiguredLoader();
        var executor = new WorkflowExecutor
        {
            EnableCheckpointing = false,
            GlobalOutputDir = root,
            TemporaryDirectory = Path.Combine(root, "tmp")
        };

        // La validación se pregunta aparte, antes de correr: así «el motor no acepta este ejemplo» es un hecho
        // propio, distinguible del «se cayó ejecutando», y ningún asiento puede taparlo.
        var validation = new GraphValidator().Validate(graph, loader);
        string? refused = validation.IsValid
            ? null
            : string.Join(" | ", validation.Errors);

        string outcome = "OK";
        string? error = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        if (refused is null)
        {
            try
            {
                await executor.ExecuteAsync(graph, loader, cts.Token);
            }
            catch (Exception ex)
            {
                outcome = ex.GetType().Name;
                error = $"{ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
            }
        }
        else
        {
            outcome = "RECHAZADO";
        }

        var produced = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => !inputs.Contains(p))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}tmp{Path.DirectorySeparatorChar}"))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var strayed = FilesIn(room).Except(roomBefore)
            .Select(p => Path.GetRelativePath(room, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var emptyArtifacts = produced
            .Where(p => new FileInfo(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar))).Length == 0)
            .ToList();

        var emptyFoldersLeft = Directory.EnumerateDirectories(Path.Combine(root, "Input"), "*", SearchOption.AllDirectories)
            .Where(d => !Directory.EnumerateFileSystemEntries(d).Any())
            .Select(d => Path.GetRelativePath(root, d).Replace('\\', '/'))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        // Qué le pasó a lo sembrado en Input/: se mira cada archivo de entrada en su ruta —¿sigue ahí?, ¿con los
        // mismos bytes?— y, para el caso de que el flujo se lo haya llevado a otro sitio, si sus bytes siguen
        // estando en algún lugar del área de trabajo. Las dos preguntas son distintas a propósito: renombrar en el
        // sitio no pierde nada, y truncar sí.
        var touchedInputs = new List<string>();
        foreach (SeededInput seeded in seededInputs)
        {
            string path = Path.Combine(root, seeded.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                touchedInputs.Add($"{seeded.RelativePath} (ya no está en su ruta)");
            }
            else if (!string.Equals(HashOf(path), seeded.Hash, StringComparison.Ordinal))
            {
                touchedInputs.Add($"{seeded.RelativePath} (con otros bytes)");
            }
        }

        var hashesInWorkspace = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(HashOf)
            .ToHashSet(StringComparer.Ordinal);
        var lostInputBytes = seededInputs
            .Where(seeded => !hashesInWorkspace.Contains(seeded.Hash))
            .Select(seeded => seeded.RelativePath)
            .ToList();

        return new Run(root, outcome, error, refused, inputs, produced, strayed, emptyArtifacts, emptyFoldersLeft,
            touchedInputs, lostInputBytes);
    }

    /// <summary>Huella de un archivo, para poder comparar sus bytes antes y después de ejecutar el flujo.</summary>
    private static string HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    /// <summary>
    /// Entrada común a todos los ejemplos: un archivo de texto y su <b>copia idéntica</b> (para los flujos de
    /// deduplicación), un CSV, una imagen de verdad, un comprimido con contenido y una carpeta anidada, más una
    /// carpeta vacía (para el limpia-carpetas). Suficiente para que cada promesa tenga con qué cumplirse.
    /// </summary>
    private static void SeedWorkspace(string root)
    {
        string input = Path.Combine(root, "Input");
        Directory.CreateDirectory(Path.Combine(input, "sub"));
        Directory.CreateDirectory(Path.Combine(input, "vacia"));
        Directory.CreateDirectory(Path.Combine(root, "Output"));

        File.WriteAllText(Path.Combine(input, "nota.txt"), "hola mundo\n");
        File.WriteAllText(Path.Combine(input, "nota-copia.txt"), "hola mundo\n");
        File.WriteAllText(Path.Combine(input, "datos.csv"), "Id,Nombre\n1,Alfa\n2,Beta\n");

        using (var image = new Image<Rgb24>(48, 48))
        {
            image.SaveAsPng(Path.Combine(input, "imagen.png"));
        }

        string tempZip = Path.Combine(root, "paquete.zip");
        using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("dentro.txt").Open());
            writer.Write("contenido del paquete\n");
        }
        File.Move(tempZip, Path.Combine(input, "paquete.zip"));

        File.WriteAllText(Path.Combine(input, "sub", "anidado.txt"), "anidado\n");
    }

    /// <summary>Los archivos que hay en un directorio, de forma recursiva (la sala limpia del banco).</summary>
    private static HashSet<string> FilesIn(string directory) =>
        [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)];

    private static string ExamplePath(string flow) =>
        Directory.EnumerateFiles(ExamplesDirectory(), $"{flow}.json", SearchOption.AllDirectories).Single();

    private static string ExamplesDirectory() =>
        Path.Combine(TestRepositoryLocator.RepositoryRoot(), "docs", "examples");

    private static List<string> ExampleFiles() =>
        [.. Directory.GetFiles(ExamplesDirectory(), "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)];
}
