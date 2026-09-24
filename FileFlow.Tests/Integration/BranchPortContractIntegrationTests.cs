using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Integration;

/// <summary>
/// El <b>contrato de salida de las ramas que no son el camino feliz</b>: un error de descompresión, un nombre de
/// tabla inseguro, un renombrado omitido o un origen que no existe tienen que salir por un puerto
/// <b>declarado</b>, con el ítem entero y con su diagnóstico.
///
/// <para>Existe porque esas ramas no las recorría nadie. Los nodos de los tres casos emitían por puertos que no
/// declaraban (<c>ItemOut</c>, <c>Error</c> y <c>Skipped</c> según el nodo), lo que en el motor significa que el
/// ítem se da por terminado sin llegar a ningún nodo y sin un solo aviso; las pruebas que había llamaban al nodo
/// con un contexto simulado que acepta cualquier nombre, así que no podían verlo. Aquí cada rama se ejecuta con
/// el motor y el cableado reales: el andamiaje es <see cref="BranchPortHarness"/> y el nombre del puerto por el
/// que llega el ítem es, en sí mismo, la afirmación.</para>
///
/// <para>Las ramas de visión viven en <c>AiVisionBranchPortIntegrationTests</c>, en la colección exclusiva
/// <c>OnnxInference</c>: el motor consulta la aceleración del nodo al terminarlo y eso toca los registros de
/// sesiones del clúster, que no se pueden leer desde una colección paralela.</para>
///
/// <para>Cada rama de aquí está declarada en <see cref="NodePortInventory"/>, y la guardia
/// <c>NodePortCoverageGuardTests</c> falla si aparece en el producto un puerto —de rama o del camino feliz—
/// sin la prueba que lo cubra.</para>
/// </summary>
[Collection(BranchPortHarnessCollection.Name)]
public class BranchPortContractIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // ArchiveFanOutNode: dos ramas de error distintas
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnArchiveThatCannotBeExtracted_ShouldLeaveByTheDeclaredErrorPort_WithItsDiagnosis()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string brokenArchive = harness.WriteFile("roto.cbz", "esto no es un archivo comprimido");

        var received = await harness.RunAsync(
            sourcePaths: [brokenArchive],
            nodeTypeName: "ArchiveFanOutNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["OutputDirectory"] = harness.WorkingRoot + Path.DirectorySeparatorChar,
                ["ArchiveFormat"] = "Auto"
            },
            branchPort: "Error");

        received.Should().ContainSingle("el archivo no se pudo descomprimir: sale por Error una vez")
            .Which.Branch.Should().Be("Branch", "el espía lo recibe por la entrada que el cable declara desde Error");
        received[0].Item.CurrentPath.Should().Be(brokenArchive, "el ítem que sale por Error es el archivo que falló");
        received[0].Item.Metadata.Should().ContainKey("RelatedVolumeFiles",
            "la rama de excepción deja el diagnóstico de volúmenes relacionados en el ítem");
        received[0].Item.Metadata["IsMultipartArchive"].Should().Be(false, "es un solo archivo, no un volúmen múltiple");
    }

    [Fact]
    public async Task AnArchiveWithoutFiles_ShouldAlsoLeaveByTheDeclaredErrorPort_ButWithoutVolumeDiagnosis()
    {
        await using var harness = await BranchPortHarness.CreateAsync();

        string emptyArchive = Path.Combine(harness.Root, "vacio.cbz");
        using (var archive = ZipFile.Open(emptyArchive, ZipArchiveMode.Create))
        {
            archive.CreateEntry("carpeta/");
        }

        var received = await harness.RunAsync(
            sourcePaths: [emptyArchive],
            nodeTypeName: "ArchiveFanOutNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["OutputDirectory"] = harness.WorkingRoot + Path.DirectorySeparatorChar,
                ["ArchiveFormat"] = "Auto"
            },
            branchPort: "Error");

        received.Should().ContainSingle("un comprimido sin ficheros no puede alimentar a nadie: sale por Error")
            .Which.Item.Metadata.Should().NotContainKey("RelatedVolumeFiles",
                "esta rama no pasó por una excepción de extracción: es la rama del comprimido vacío, que no diagnostica volúmenes");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ArchiveFanInNode: el empaquetado que no puede escribir su destino
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AFanInThatCannotCreateItsDestination_ShouldLeaveByTheDeclaredErrorPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string page = harness.WriteFile("pagina.txt", "una pagina");
        harness.WriteFile("bloqueo", "no soy una carpeta");

        var received = await harness.RunAsync(
            sourcePaths: [page],
            nodeTypeName: "ArchiveFanInNode",
            nodeParameters: new Dictionary<string, object?>
            {
                // La carpeta de destino cuelga de un fichero: crear el directorio no puede funcionar, y es el
                // único fallo del empaquetado que se provoca sin depender del sistema de archivos anfitrión ni
                // de sus permisos.
                ["DestinationFolder"] = Path.Combine(harness.Root, "bloqueo", "salida"),
                ["ArchiveName"] = "{Archive:OriginalArchiveFileName}"
            },
            branchPort: "Error",
            itemMetadata: "Archive:SessionId=sesion-1;Archive:TotalEntries=1;Archive:OriginalArchiveFileName=comic.cbz;Archive:OriginalArchiveFormat=ZIP");

        received.Should().ContainSingle("el empaquetado no pudo crear la carpeta de destino: sale por Error")
            .Which.Branch.Should().Be("Branch");
        received[0].Item.Metadata.Should().ContainKey("Archive:OriginalArchiveFileName",
            "el ítem que sale por Error es el que abrió la sesión, con su rastro de archivo original");
        received[0].Item.ExecutionLog.Should().Contain(l => l.Contains("ArchiveFanInNode error"),
            "el diagnóstico del fallo viaja en el ítem, no solo en el log de la ejecución");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SqliteDatabaseSinkNode
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("AuditTrail; DROP TABLE Users; --")]
    [InlineData("123_StartsWithDigit")]
    public async Task ASinkWithAnUnsafeTableName_ShouldLeaveByTheDeclaredErrorPort_AndNotTouchTheDatabase(string tableName)
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string existingFile = harness.WriteFile("documento.txt", "contenido");
        string databasePath = Path.Combine(harness.Root, "auditoria.db");

        var received = await harness.RunAsync(
            sourcePaths: [existingFile],
            nodeTypeName: "SqliteDatabaseSinkNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["DatabasePath"] = databasePath,
                ["TableName"] = tableName,
                ["AutoCreateTable"] = true
            },
            branchPort: "Error");

        received.Should().ContainSingle().Which.Branch.Should().Be("Branch");
        received[0].Item.FileName.Should().Be("documento.txt");
        File.Exists(databasePath).Should().BeFalse(
            "el nombre de tabla se valida antes de abrir nada: la rama de error no puede dejar rastro en el disco");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // AdvancedRenamerNode: las dos ramas de la colisión
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheFilesWhoseTargetNameIsAlreadyTaken_ShouldLeaveByTheDeclaredSkippedPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string first = harness.WriteFile("uno.txt", "uno");
        string second = harness.WriteFile("dos.txt", "dos");

        // El nombre al que apuntan los dos ya está ocupado en el disco: es la colisión que la estrategia `Skip`
        // resuelve omitiendo, sin carrera posible con el otro archivo del lote. Que los dos se omitan es también
        // el contrato de que la configuración se leyó <b>una</b> vez: con la migración de parámetros legados sin
        // proteger, el segundo archivo del lote caía en la plantilla por omisión y se renombraba a un nombre que
        // nadie había configurado (el motor entrega los ítems en paralelo sobre el mismo nodo).
        harness.WriteFile("comun.txt", "ya estaba aquí");

        var received = await harness.RunAsync(
            sourcePaths: [first, second],
            nodeTypeName: "AdvancedRenamerNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["Pattern"] = "comun.txt",
                ["RenameMode"] = "Virtual",
                ["CollisionStrategy"] = "Skip"
            },
            branchPort: "Skipped");

        received.Should().HaveCount(2, "los dos archivos apuntan a un nombre ocupado");
        received.Should().OnlyContain(r => r.Branch == "Branch",
            "nada sale por el camino feliz cuando todos los destinos están ocupados");
        received.Select(r => Path.GetFileName(r.Item.CurrentPath)).Should().BeEquivalentTo(["uno.txt", "dos.txt"],
            "un archivo omitido conserva su nombre: no se renombró a medias");
        File.Exists(first).Should().BeTrue("el modo Virtual no toca el disco");
        File.Exists(second).Should().BeTrue();
    }

    [Fact]
    public async Task ARenamerWhoseFailStrategyFindsTheNameOccupied_ShouldLeaveByTheDeclaredErrorPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string first = harness.WriteFile("uno.txt", "uno");
        string second = harness.WriteFile("dos.txt", "dos");
        string occupied = harness.WriteFile("comun.txt", "ya estaba aquí");

        // La estrategia `Fail` convierte la colisión en excepción dentro del `try` del nodo, así que el ítem sale
        // por Error con su diagnóstico. Es la rama que faltaba: la cobertura de `Error` que había era el origen
        // inexistente, que se decide <b>antes</b> de entrar al renombrado y no llega a mirar la colisión.
        var received = await harness.RunAsync(
            sourcePaths: [first, second],
            nodeTypeName: "AdvancedRenamerNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["Pattern"] = "comun.txt",
                ["RenameMode"] = "Virtual",
                ["CollisionStrategy"] = "Fail"
            },
            branchPort: "Error");

        received.Should().HaveCount(2, "los dos archivos del lote chocan con el mismo nombre ocupado");
        received.Should().OnlyContain(r => r.Branch == "Branch", "nada sale por el camino feliz: los dos fallan");
        received.Select(r => Path.GetFileName(r.Item.CurrentPath)).Should().BeEquivalentTo(["uno.txt", "dos.txt"],
            "un renombrado que falla no deja el ítem a medio renombrar");
        received.SelectMany(r => r.Item.ExecutionLog).Should().Contain(l => l.Contains("Target file already exists"),
            "el diagnóstico del choque viaja en el ítem");
        File.ReadAllText(occupied).Should().Be("ya estaba aquí", "el archivo que ocupaba el nombre no se toca");
    }

    [Fact]
    public async Task ARenamerThatCannotFindItsSource_ShouldLeaveByTheDeclaredErrorPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string missing = Path.Combine(harness.Root, "no_existe.txt");

        var received = await harness.RunAsync(
            sourcePaths: [missing],
            nodeTypeName: "AdvancedRenamerNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["Pattern"] = "{FileNameNoExt}_ok.{Ext}",
                ["RenameMode"] = "DirectInPlace"
            },
            branchPort: "Error");

        received.Should().ContainSingle().Which.Item.CurrentPath.Should().Be(missing,
            "el ítem que no se pudo renombrar sale por Error conservando su ruta original");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La familia «el archivo de entrada no está»: un caso por nodo, la misma rama
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los nodos cuya rama de error se alcanza porque <b>la entrada no existe en el disco</b>: el fallo que
    /// comparten los plugins cuando el flujo llega con una ruta que ya no está. El segundo valor es el puerto del
    /// camino feliz —cada nodo nombra el suyo: <c>Out</c>, <c>Done</c>, <c>Deleted</c>— y el tercero la rama que
    /// se afirma.
    ///
    /// <para>Añadir un nodo aquí es barato mientras su rama de error sea esta; si el caso necesita parámetros, se
    /// le dan en el cuerpo de la prueba por nombre de nodo, no cambiando la teoría.</para>
    /// </summary>
    public static TheoryData<string, string, string> NodesThatFailBecauseTheirInputIsMissing => new()
    {
        { "ArchiveCompressorNode", "Out", "Error" },
        { "SmartUnpackNode", "Out", "Error" },
        { "DestinationSinkNode", "Done", "Error" },
        { "FileRelocatorNode", "Out", "Error" },
        { "OriginalFileActionNode", "Out", "Error" },
        { "SafeRecycleDeleteNode", "Deleted", "Error" },
        { "DocumentProcessorNode", "Out", "Error" },
        { "HashCalculatorNode", "Out", "Error" },
        { "DeduplicationFilterNode", "Unique", "Error" },
        { "MediaTranscoderNode", "Out", "Error" },
        { "ImageOptimizerNode", "Out", "Error" },
        { "NetworkUploadNode", "Out", "Error" }
    };

    [Theory]
    [MemberData(nameof(NodesThatFailBecauseTheirInputIsMissing))]
    public async Task ANodeThatCannotFindItsInput_ShouldLeaveByItsDeclaredBranchPort(
        string nodeTypeName,
        string happyPort,
        string branchPort)
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string missing = Path.Combine(harness.Root, "no_existe.txt");

        var received = await harness.RunAsync(
            sourcePaths: [missing],
            nodeTypeName: nodeTypeName,
            nodeParameters: new Dictionary<string, object?>(),
            branchPort: branchPort,
            happyPort: happyPort);

        received.Should().ContainSingle($"{nodeTypeName} tiene que salir una vez por '{branchPort}'")
            .Which.Branch.Should().Be("Branch",
                $"{nodeTypeName} emite por '{branchPort}', que es el puerto que el cable declara hacia el espía");
        received[0].Item.CurrentPath.Should().Be(missing,
            $"{nodeTypeName} no puede inventarse una ruta: el ítem que sale por la rama es el que entró");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Integraciones: el proceso externo y la notificación HTTP
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ACliNodeWhoseExecutableDoesNotExist_ShouldLeaveByTheDeclaredFailedPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.txt", "contenido");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: "CliExecutionNode",
            nodeParameters: new Dictionary<string, object?>
            {
                // Un ejecutable que no existe en ningún sistema: lanzarlo lanza, y el nodo lo convierte en
                // `Failed`. No se usa un comando que devuelva un código de error porque eso dependería del
                // intérprete del anfitrión y de sus argumentos.
                ["ExecutablePath"] = "fileflow_no_existe_9f3a",
                ["ArgumentsTemplate"] = ""
            },
            branchPort: "Failed",
            happyPort: "Success");

        received.Should().ContainSingle("el proceso no se pudo lanzar: sale por Failed")
            .Which.Branch.Should().Be("Branch");
        received[0].Item.CurrentPath.Should().Be(file);
    }

    /// <summary>
    /// Los dos nodos de red que delegan en la fábrica de transportes: cuando el protocolo de la configuración no
    /// tiene estrategia, la fábrica lanza y el nodo convierte el fallo en su rama de error. Se prueba sin red y
    /// sin servidor ajeno: el rechazo ocurre antes de abrir ninguna conexión.
    /// </summary>
    public static TheoryData<string> NodesThatRejectAnUnsupportedProtocol => new()
    {
        { "NetworkUploadNode" },
        { "NetworkDownloadNode" }
    };

    [Theory]
    [MemberData(nameof(NodesThatRejectAnUnsupportedProtocol))]
    public async Task ANodeWithAnUnsupportedProtocol_ShouldLeaveByTheDeclaredErrorPort(string nodeTypeName)
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.txt", "contenido");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: nodeTypeName,
            nodeParameters: new Dictionary<string, object?>
            {
                ["Protocol"] = "PROTOCOLO_QUE_NO_EXISTE",
                ["DestinationFolder"] = harness.Root
            },
            branchPort: "Error");

        received.Should().ContainSingle($"{nodeTypeName} no tiene estrategia para ese protocolo: sale por Error")
            .Which.Branch.Should().Be("Branch");
        received[0].Item.CurrentPath.Should().Be(file, "el ítem que sale por la rama es el que entró");
    }

    [Fact]
    public async Task AWebhookWithAnUnsupportedUrl_ShouldLeaveByTheDeclaredFailedPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.txt", "contenido");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: "WebhookNotificationNode",
            nodeParameters: new Dictionary<string, object?>
            {
                // Sin esquema http(s): se descarta antes de abrir ninguna conexión, así que la rama se prueba sin
                // red y sin depender de que un servidor ajeno conteste.
                ["Url"] = "fileflow-no-es-una-url"
            },
            branchPort: "Failed");

        received.Should().ContainSingle("la URL no es utilizable: sale por Failed").Which.Branch.Should().Be("Branch");
        received[0].Item.CurrentPath.Should().Be(file);
    }
}
