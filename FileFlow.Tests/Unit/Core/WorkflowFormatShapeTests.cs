using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// Guardia de la <b>versión del formato</b>: falla si el modelo del flujo gana o pierde un campo sin subir la
/// versión, señalando qué campo apareció o desapareció.
///
/// <para>
/// Por qué existe: la versión del formato se usa para <b>reparar</b> archivos antiguos y para no sobrescribir
/// los de una versión posterior (fase 3A), y las dos cosas suponen que una versión distinta escribe una forma
/// distinta. Hasta aquí eso lo sostenía la disciplina: nada impedía añadir un campo al modelo dejando el
/// <c>schema</c> en <c>v2</c>, y entonces un archivo con el campo nuevo y otro sin él se declaran la misma
/// versión —el de la versión anterior lo lee todo, o pierde en silencio lo que no conoce, y el de la posterior
/// se repara como si fuera antiguo—. Un formato que cambia de forma sin decirlo no está versionado: está
/// numerado.
/// </para>
///
/// <para>
/// Cómo lo mide: escribe un grafo que rellena <b>todos</b> los campos del modelo y registra la forma del
/// archivo resultante —cada sendero con su tipo JSON— como la forma de la versión actual. La lista de senderos
/// de una misma versión es un contrato: si cambia, la versión tiene que cambiar con ella. La forma se lee del
/// texto escrito por el escritor único (<see cref="WorkflowFormatShape"/>), no de la reflexión sobre el modelo,
/// y por eso el registro de la versión es lo que se compara; la reflexión se usa para lo contrario, para exigir
/// que <b>ninguna</b> propiedad declarada se quede sin escribir —el campo añadido y olvidado, que es la forma
/// silenciosa del mismo error—.
/// </para>
///
/// <para>
/// Y una fila no se sostiene sola: cada versión tiene su <b>archivo testigo</b>
/// (<see cref="FlowFormatWitness"/>) —un flujo de verdad, guardado por el escritor de esa versión y comprometido
/// en el repositorio—, y la fila tiene que decir lo que ese archivo dice. Es lo que hace que reescribir la fila
/// a mano falle en vez de pasar en silencio: el archivo no lo escribió quien edita la prueba. Y hay una segunda
/// ganancia, que no era el objetivo: el flujo de la muestra con la que se mide la forma <b>es</b> el que
/// contienen los testigos, así que no hay dos referencias que mantener —si fueran dos, un campo nuevo habría que
/// añadirlo en ambas y nada diría si se hizo en la que importa—.
/// </para>
///
/// <para>
/// Límites declarados. El <b>diccionario</b> de parámetros no se mide: sus claves las pone quien guarda, no el
/// formato. Y un cambio de lo que se escribe que edite <b>a la vez</b> la fila registrada y el archivo testigo
/// pasa: los dos viven en el repositorio y ningún test puede atestiguar por su cuenta qué escribió una versión
/// que ya no se ejecuta. Lo que sí garantiza la guardia es que ese camino son dos ediciones deliberadas —una
/// fila en una prueba y un archivo de flujo comprometido, que el producto abre— en vez de una línea que se cuela.
/// </para>
/// </summary>
public class WorkflowFormatShapeTests
{
    /// <summary>
    /// Senderos cuyo objeto es un diccionario de claves puestas por quien guarda —los parámetros del nodo, que
    /// incluyen la definición incrustada de un subflujo, un formato dentro del formato—. No son campos de éste.
    /// </summary>
    private static readonly string[] Dictionaries = ["nodes[].parameters"];

    /// <summary>
    /// La forma que escribe cada versión del formato, tal y como se entregó.
    ///
    /// <para>
    /// La fila de una versión <b>no se reescribe</b>: describe un archivo que ya existe en disco y que las
    /// versiones siguientes siguen teniendo que interpretar del mismo modo. Cuando el modelo gane o pierda un
    /// campo, se sube <c>WorkflowFormat.CurrentSchema</c>/<c>CurrentVersion</c> y se añade la fila de la versión
    /// nueva con la forma que la prueba imprime al fallar. Se reescribe la fila de una versión entregada sólo si
    /// el error fue declarar mal lo que esa versión escribía, y entonces se dice por qué en el mensaje del
    /// commit: no es un cambio de forma, es una corrección del registro.
    /// </para>
    /// </summary>
    private static readonly Dictionary<int, string[]> ShippedShapes = new()
    {
        [2] =
        [
            "annotations[].color : String",
            "annotations[].content : String",
            "annotations[].height : Number",
            "annotations[].id : String",
            "annotations[].title : String",
            "annotations[].width : Number",
            "annotations[].x : Number",
            "annotations[].y : Number",
            "breakpointNodeIds[] : String",
            "disabledLoggingNodeIds[] : String",
            "edges[].id : String",
            "edges[].sourceNodeId : String",
            "edges[].sourcePortName : String",
            "edges[].targetNodeId : String",
            "edges[].targetPortName : String",
            "globalOutputDir : String",
            "groups[].color : String",
            "groups[].height : Number",
            "groups[].id : String",
            "groups[].nodeIds[] : String",
            "groups[].title : String",
            "groups[].width : Number",
            "groups[].x : Number",
            "groups[].y : Number",
            "name : String",
            "nodes[].customTitle : String",
            "nodes[].hasBreakpoint : Boolean",
            "nodes[].id : String",
            "nodes[].isLoggingEnabled : Boolean",
            "nodes[].nodeTypeName : String",
            "nodes[].parameters.{*} : datos",
            "nodes[].x : Number",
            "nodes[].y : Number",
            "schema : String",
            "temporaryDirectory : String"
        ]
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // El formato contra su versión
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La prueba que da nombre al fichero: la forma que el escritor produce hoy tiene que ser exactamente la
    /// que declara la versión actual. Un campo de más o de menos —o del que cambia el tipo— la pone en rojo.
    /// </summary>
    [Fact]
    public void TheCurrentVersion_ShouldOwnTheRecordedShape()
    {
        var live = LiveShape();

        // La fila puede no estar todavía: subir la versión y pegar después su forma es el orden natural, y el
        // fallo tiene que decir qué falta en vez de reventar por el índice.
        ShippedShapes.Should().ContainKey(WorkflowFormat.CurrentVersion, because:
            $"`schema` dice v{WorkflowFormat.CurrentVersion} y el registro no tiene su fila: añádela con esta " +
            $"forma, que es la que el escritor produce hoy{Environment.NewLine}{Environment.NewLine}" +
            WorkflowFormatShape.AsRecordLiteral(live));

        var recorded = ShippedShapes[WorkflowFormat.CurrentVersion];
        var difference = WorkflowFormatShape.Compare(recorded, live);

        live.Should().Equal(recorded, because:
            $"""
            la forma del archivo de flujo cambió y `schema` sigue diciendo v{WorkflowFormat.CurrentVersion}: un
            archivo con un campo que la versión declara y otro sin él se leerían como el mismo formato.

            {difference}

            Si el cambio es deliberado, es una versión nueva: sube `WorkflowFormat.CurrentSchema` a
            `FileFlow.Workflow.v{WorkflowFormat.CurrentVersion + 1}` —y `WorkflowFormat.CurrentVersion` con
            ella— y añade su fila al registro de este fichero con esta forma, sin tocar la de
            v{WorkflowFormat.CurrentVersion}:

            {WorkflowFormatShape.AsRecordLiteral(live)}

            Si editas una fila ya entregada, el testigo de esa versión lo dirá: lo que esa versión escribió está
            en `FileFlow.Tests/FormatBaselines`.
            """);
    }

    /// <summary>
    /// La otra mitad: el registro tiene que tener una fila por cada versión que se ha entregado, y dos versiones
    /// distintas tienen que describir formas distintas —si no, el número sube sin que el archivo cambie, que es
    /// lo contrario del mismo error—. Así, subir la versión exige registrar su forma, y registrar una forma
    /// nueva exige haber subido la versión.
    /// </summary>
    [Fact]
    public void EveryShippedVersion_ShouldHaveItsShape_AndNoTwo_ShouldShareOne()
    {
        var expected = Enumerable
            .Range(WorkflowFormat.UndeclaredVersion + 1, WorkflowFormat.CurrentVersion - WorkflowFormat.UndeclaredVersion)
            .ToList();

        ShippedShapes.Keys.Should().BeEquivalentTo(expected,
            "cada versión entregada del formato tiene que estar registrada, desde la que aún se escribe —" +
            $"v{WorkflowFormat.CurrentVersion}— hasta la primera versionada, y ninguna otra: una fila para una " +
            "versión que no existe es una versión que se anuncia y no se ha entregado");

        var duplicates = ShippedShapes
            .GroupBy(shape => string.Join('\n', shape.Value), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(shape => $"v{shape.Key}")))
            .ToList();

        duplicates.Should().BeEmpty(
            "dos versiones con la misma forma no son dos versiones: subir el número sin cambiar el archivo hace " +
            "que se le apliquen reparaciones de un formato anterior y que se proteja de una pérdida que no existe");
    }

    /// <summary>
    /// El campo añadido y <b>olvidado</b>: el que no se escribe porque nadie lo puso, con lo que la forma no
    /// cambia y la prueba anterior no lo ve. La reflexión sí: toda propiedad pública del modelo tiene que
    /// aparecer en el archivo, salvo que se marque ignorada a propósito y se diga por qué.
    /// </summary>
    [Fact]
    public void NoPropertyOfTheModel_ShouldBeAbsentFromTheFile()
    {
        var root = JsonDocument
            .Parse(FlowFormatWitness.Flow().ToJson())
            .RootElement;

        var unwritten = ModelParts(root)
            .SelectMany(part => UnwrittenProperties(part.Type, part.Element))
            .ToList();

        unwritten.Should().BeEmpty(
            "el modelo del flujo es lo que el archivo escribe: una propiedad que no llega al archivo —o porque " +
            "es nula en el grafo de muestra, que es lo que pasa cuando nadie la rellena, o porque la escritura " +
            "la salta— desaparece al guardar y vuelve al releer. Si el campo forma parte del formato, se " +
            "escribe y la versión sube; si no lo forma parte, se marca [JsonIgnore] y el motivo se escribe aquí " +
            "junto a la propiedad");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El archivo testigo de cada versión
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Las versiones registradas, como casos de prueba: cada una tiene su fila y su archivo testigo.</summary>
    public static IEnumerable<object[]> RecordedVersions =>
        ShippedShapes.Keys.OrderBy(version => version).Select(version => new object[] { version });

    /// <summary>
    /// La fila no es lo único que describe una versión: el archivo que esa versión escribió también, y dicen lo
    /// mismo. Si la fila cambia y el archivo no —que es exactamente lo que hace reescribirla a mano— lo que
    /// cambió es el registro, no el formato: la fila pasaría a describir un archivo que nadie escribió.
    /// </summary>
    [Theory]
    [MemberData(nameof(RecordedVersions))]
    public void TheShapeOfAWitness_ShouldBeTheRecordedOne(int version)
    {
        string path = FlowFormatWitness.PathOf(version);
        string text = FlowFormatWitness.Text(version);

        WorkflowFormatShape.Describe(text, Dictionaries).Should().Equal(ShippedShapes[version], because:
            $"'{RelativeToRepository(path)}' es el archivo que escribió la v{version}, y la fila registrada de esa " +
            "versión tiene que decir lo que ese archivo dice. Devuélvela a lo que el archivo dice; y si de verdad " +
            "se escribió algo distinto, eso es una versión nueva con su propio testigo, nunca una fila editada de " +
            "una versión que ya se entregó");

        DeclaredVersionOf(text).Should().Be(version, because:
            $"'{RelativeToRepository(path)}' se llama testigo de la v{version}, así que su `schema` tiene que " +
            "decir esa misma versión: un archivo cuyo nombre y cuyo contenido no dicen lo mismo no es el testigo " +
            "de ninguna");
    }

    /// <summary>
    /// Cada versión registrada tiene su testigo, y cada testigo es de una versión registrada: una fila sin
    /// archivo es un registro que nadie ha escrito de verdad —y de una versión que ya no se escribe no se puede
    /// fabricar—, y un archivo sin fila es un flujo que nadie mira.
    /// </summary>
    [Fact]
    public void EveryRecordedVersion_ShouldHaveItsWitness_AndNoWitness_ShouldBeOfAnotherVersion()
    {
        FlowFormatWitness.VersionsPresent().Should().BeEquivalentTo(ShippedShapes.Keys, because:
            "una versión registrada sin archivo testigo es una fila que nadie ha escrito de verdad, y un testigo " +
            "sin fila es un archivo sin registro: los dos viven en el repositorio y tienen que cubrirse");

        FlowFormatWitness.Documents()
            .Where(document => document.Version <= 0)
            .Select(document => RelativeToRepository(document.Path))
            .Should().BeEmpty(
                $"'{RelativeToRepository(FlowFormatWitness.DirectoryPath)}' sólo contiene testigos, uno por " +
                "versión, y su nombre dice cuál: un archivo suelto ahí no es el testigo de nada y no lo mira nadie");
    }

    /// <summary>
    /// Mientras una versión es la que se escribe, su testigo tiene que ser lo que el escritor produce hoy para
    /// ese mismo flujo —se compara el documento, no el formateo—: el testigo es la prueba de que el formato es
    /// uno, y un archivo comprometido que dejara de ser lo que el producto escribe atestiguaría una versión
    /// pasada con la forma de hoy.
    /// </summary>
    [Fact]
    public void TheWitnessOfTheCurrentVersion_ShouldBeWhatTheWriterProduces()
    {
        string path = FlowFormatWitness.PathOf(WorkflowFormat.CurrentVersion);

        bool sameDocument = JsonNode.DeepEquals(
            JsonNode.Parse(FlowFormatWitness.Text(WorkflowFormat.CurrentVersion)),
            JsonNode.Parse(FlowFormatWitness.ProducedText()));

        sameDocument.Should().BeTrue(because:
            $"'{RelativeToRepository(path)}' es el testigo de la versión que se escribe, así que tiene que ser " +
            "exactamente lo que el escritor produce para ese flujo. Si el cambio es deliberado, es una versión " +
            "nueva con su testigo: el de ésta se queda como está, porque describe lo que se entregó");
    }

    /// <summary>
    /// El testigo tiene que ser un flujo que el producto <b>abre</b>, no un texto con la forma adecuada: si el
    /// lector no lo entiende, lo que se está anclando es un archivo que nadie puede usar. Se lee por el camino
    /// de carga de la aplicación y se comprueba lo que se guardó, incluidos los tipos que el lector infiere de
    /// los parámetros y el parámetro <b>nulo</b>, que el formato sí escribe.
    /// </summary>
    [Theory]
    [MemberData(nameof(RecordedVersions))]
    public async Task TheProduct_ShouldReadTheWitnessAsTheFlowItContains(int version)
    {
        string path = FlowFormatWitness.PathOf(version);
        var expected = FlowFormatWitness.Flow();

        var loaded = await new WorkflowStorageService().LoadWorkflowAsync(path);

        loaded.Name.Should().Be(expected.Name);
        loaded.GlobalOutputDir.Should().Be(expected.GlobalOutputDir);
        loaded.TemporaryDirectory.Should().Be(expected.TemporaryDirectory);
        WorkflowFormat.VersionOf(loaded).Should().Be(version, "el archivo declara su versión, y el lector la lee");

        loaded.Nodes.Should().HaveCount(2);
        loaded.Nodes[0].Id.Should().Be("nodo-1");
        loaded.Nodes[0].NodeTypeName.Should().Be("FileCopy");
        loaded.Nodes[0].CustomTitle.Should().Be("Copia los recibos");
        loaded.Nodes[0].HasBreakpoint.Should().BeTrue();
        loaded.Nodes[0].IsLoggingEnabled.Should().BeTrue();
        loaded.Nodes[1].IsLoggingEnabled.Should().BeFalse("un `false` también se escribe");

        loaded.Nodes[0].Parameters.Should().ContainKey("RutaOrigen").WhoseValue.Should().Be(@"C:\Entrada");
        loaded.Nodes[0].Parameters.Should().ContainKey("Reintentos")
            .WhoseValue.Should().Be(3L, "el lector infiere el número como entero de 64 bits");
        loaded.Nodes[0].Parameters.Should().ContainKey("Sobrescribir").WhoseValue.Should().Be(true);
        loaded.Nodes[0].Parameters.Should().ContainKey("Etiqueta")
            .WhoseValue.Should().BeNull("un valor nulo dentro del diccionario sí se escribe: la omisión es de " +
                "propiedades");

        loaded.Edges.Should().ContainSingle();
        loaded.Edges[0].SourceNodeId.Should().Be("nodo-1");
        loaded.Edges[0].SourcePortName.Should().Be("Done");
        loaded.Edges[0].TargetNodeId.Should().Be("nodo-2");
        loaded.Edges[0].TargetPortName.Should().Be("In");

        loaded.Annotations.Should().ContainSingle();
        loaded.Annotations[0].Title.Should().Be("Recuerda");
        loaded.Annotations[0].Color.Should().Be("#FEF08A");

        loaded.Groups.Should().ContainSingle();
        loaded.Groups[0].NodeIds.Should().BeEquivalentTo("nodo-1", "nodo-2");

        loaded.BreakpointNodeIds.Should().BeEquivalentTo("nodo-1");
        loaded.DisabledLoggingNodeIds.Should().BeEquivalentTo("nodo-2");
    }

    /// <summary>
    /// La política de escritura, caso por caso: la única escritura legítima es la del testigo de la versión que
    /// aún no tiene uno —y sólo mientras es la versión que se escribe—.
    ///
    /// <para>
    /// Un testigo <b>no</b> tiene regeneración, y por eso no hay variable de entorno que lo permita, al
    /// contrario que las líneas base visuales: regenerar una captura es aceptar un cambio de aspecto, y
    /// regenerar un testigo haría que un archivo de la v2 declarara cosas que la v2 no escribía. Lo que se
    /// acepta con una versión nueva es un testigo nuevo.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(3, false, 2, WitnessWrite.Refuse)]  // no existe un escritor de la v3 que lo escriba
    [InlineData(3, true, 2, WitnessWrite.Refuse)]
    [InlineData(1, false, 2, WitnessWrite.Refuse)]  // anterior y sin testigo: se explica, no se rellena
    [InlineData(1, true, 2, WitnessWrite.Refuse)]
    [InlineData(2, true, 2, WitnessWrite.Leave)]    // la que se escribe, con testigo: se lee
    [InlineData(2, false, 2, WitnessWrite.Create)]  // la que se escribe, sin testigo: se escribe y se revisa
    public void WritingAWitness_ShouldBeAllowedOnlyForTheVersionBeingWritten(
        int version, bool exists, int currentVersion, WitnessWrite expected)
    {
        FlowFormatWitness.WriteDecision(version, exists, currentVersion).Should().Be(expected);

        if (expected == WitnessWrite.Refuse)
        {
            FlowFormatWitness.WhyNotWrite(version, exists, currentVersion)
                .Should().Contain($"v{version}", "quien lo intenta tiene que saber de qué versión se habla");
        }
    }

    /// <summary>
    /// La negativa, de verdad y no sólo en la tabla: el testigo de la versión <b>sin versionar</b> no se
    /// fabrica —el formato existía antes de numerarse, así que su testigo también sería un archivo real, y
    /// escribirlo con el escritor de hoy daría un archivo con la forma de hoy—, y negarse no deja nada a medias.
    /// </summary>
    [Fact]
    public void MissingWitnessOfAnEarlierVersion_ShouldNotBeFabricated()
    {
        int earlier = WorkflowFormat.UndeclaredVersion;
        string path = FlowFormatWitness.PathOf(earlier);

        File.Exists(path).Should().BeFalse(
            $"un testigo de la v{earlier} no se puede fabricar, así que no puede existir");

        var act = () => FlowFormatWitness.Text(earlier);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*v{earlier}*", "la negativa tiene que decir de qué versión habla");

        File.Exists(path).Should().BeFalse("negarse no puede dejar un archivo a medias");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-pruebas del medidor
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Que la forma distingue lo escrito de lo omitido es todo lo que hace falta para que la prueba principal
    /// signifique algo: un campo nulo no se escribe —el formato ignora los nulos—, así que éste es exactamente
    /// el cambio que se quiere ver.
    /// </summary>
    [Fact]
    public void TheShape_ShouldTellAWrittenFieldFromAnOmittedOne()
    {
        string withoutTitle = """{ "nodes": [ { "id": "nodo-1" } ] }""";
        string withTitle = """{ "nodes": [ { "id": "nodo-1", "customTitle": "Título" } ] }""";

        var difference = WorkflowFormatShape.Compare(
            WorkflowFormatShape.Describe(withoutTitle),
            WorkflowFormatShape.Describe(withTitle));

        difference.Added.Should().Equal(["nodes[].customTitle : String"]);
        difference.Removed.Should().BeEmpty();
        difference.ToString().Should().Contain("customTitle", "el fallo tiene que nombrar el campo que apareció");

        // Y al revés —un campo que se pierde— el mismo mecanismo lo señala en el otro sentido.
        var backwards = WorkflowFormatShape.Compare(
            WorkflowFormatShape.Describe(withTitle),
            WorkflowFormatShape.Describe(withoutTitle));

        backwards.Removed.Should().Equal(["nodes[].customTitle : String"]);
        backwards.Added.Should().BeEmpty();
    }

    /// <summary>
    /// La forma es del <b>formato</b>, no de los datos: los valores de un flujo concreto no la cambian, ni
    /// tampoco las claves de sus parámetros, que las pone quien guarda. Sin esto, el registro de la versión
    /// dependería de la muestra —y cambiar la muestra pediría subir la versión sin que el formato cambiara—.
    /// </summary>
    [Fact]
    public void TheShape_ShouldNotDependOnTheValuesOfTheFlow()
    {
        var one = FlowFormatWitness.Flow();
        one.Name = "Flujo uno";
        one.Nodes[0].X = 1.5;
        one.Nodes[0].Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["RutaDeEntrada"] = @"C:\Uno",
            ["Reintentos"] = 3,
            ["Activo"] = true
        };

        var other = FlowFormatWitness.Flow();
        other.Name = "Flujo dos";
        other.Nodes[0].X = -999.25;
        other.Nodes[0].Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["otraClave"] = "otro valor"
        };

        WorkflowFormatShape.Describe(one, Dictionaries)
            .Should().Equal(WorkflowFormatShape.Describe(other, Dictionaries),
                "las claves y los valores de los parámetros son datos del flujo: si entraran en la forma, " +
                "cualquier flujo guardado pediría subir la versión del formato");

        // Y la entrada del diccionario está: el campo `parameters` es del formato aunque su contenido no lo sea.
        WorkflowFormatShape.Describe(one, Dictionaries)
            .Should().Contain($"nodes[].parameters.{WorkflowFormatShape.DataKeys} : {WorkflowFormatShape.DataKind}");
    }

    /// <summary>
    /// La forma anota los elementos de una lista bajo el mismo sendero —todos los nodos son del mismo tipo—, y
    /// no le da miedo un archivo que no es el de un flujo entero: el medidor se prueba con fragmentos para que
    /// probar que detecta infracciones no exija dejar infracciones en el modelo.
    /// </summary>
    [Fact]
    public void TheShape_ShouldKeepTheElementsOfAListUnderOnePath()
    {
        string json = """
            {
              "nodes": [ { "id": "a", "x": 1 }, { "id": "b", "x": 2 } ],
              "breakpointNodeIds": [ "a", "b" ],
              "annotations": [],
              "groups": [ { "nodeIds": [] } ]
            }
            """;

        var shape = WorkflowFormatShape.Describe(json);

        shape.Should().BeEquivalentTo(
        [
            "annotations[] : (lista vacía)",
            "breakpointNodeIds[] : String",
            "groups[].nodeIds[] : (lista vacía)",
            "nodes[].id : String",
            "nodes[].x : Number"
        ]);
    }

    /// <summary>
    /// El tipo del valor forma parte de la forma: cambiar un campo de número a texto sin subir la versión deja
    /// un archivo que la versión anterior no puede leer, y la diferencia tiene que verlo. Es el mismo cambio de
    /// forma que añadir un campo, dicho de otra manera.
    /// </summary>
    [Fact]
    public void TheShape_ShouldTellAFieldThatChangedItsType()
    {
        string number = """{ "nodes": [ { "x": 12.5 } ] }""";
        string text = """{ "nodes": [ { "x": "12,5" } ] }""";

        var difference = WorkflowFormatShape.Compare(
            WorkflowFormatShape.Describe(number),
            WorkflowFormatShape.Describe(text));

        difference.Added.Should().Equal(["nodes[].x : String"]);
        difference.Removed.Should().Equal(["nodes[].x : Number"]);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El flujo de referencia: la muestra de la forma y el contenido de los testigos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La forma que el escritor único produce hoy para el flujo de referencia. El grafo no vive aquí sino en
    /// <see cref="FlowFormatWitness.Flow"/>, que es el mismo que contienen los archivos testigo: dos muestras
    /// serían dos sitios donde rellenar un campo nuevo y ninguno donde notar que falta uno.
    /// </summary>
    private static IReadOnlyList<string> LiveShape() =>
        WorkflowFormatShape.Describe(FlowFormatWitness.Flow(), Dictionaries);

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>La versión que declara un archivo de flujo, leída de su <c>schema</c>.</summary>
    private static int DeclaredVersionOf(string json) =>
        WorkflowFormat.VersionOf(WorkflowFormat.DeclaredSchema(JsonDocument.Parse(json).RootElement));

    /// <summary>
    /// La ruta tal y como se escribe en el mensaje de un fallo: relativa al repositorio, que es como se nombra
    /// un archivo comprometido (una ruta absoluta de la máquina de quien lo ejecuta no dice nada a nadie).
    /// </summary>
    private static string RelativeToRepository(string path) =>
        Path.GetRelativePath(TestRepositoryLocator.RepositoryRoot(), path).Replace('\\', '/');

    /// <summary>El modelo del flujo repartido por el archivo: la raíz y el primer elemento de cada lista.</summary>
    private static IEnumerable<(Type Type, JsonElement Element)> ModelParts(JsonElement root) =>
    [
        (typeof(WorkflowGraph), root),
        (typeof(WorkflowNode), root.GetProperty("nodes")[0]),
        (typeof(WorkflowEdge), root.GetProperty("edges")[0]),
        (typeof(WorkflowAnnotation), root.GetProperty("annotations")[0]),
        (typeof(WorkflowGroup), root.GetProperty("groups")[0])
    ];

    /// <summary>
    /// Propiedades declaradas que el archivo no escribe. El nombre se deriva de la política de nombres del
    /// formato —la del escritor único—, no de una copia: si el formato escribe en camelCase, la comprobación
    /// tiene que buscar en camelCase.
    /// </summary>
    private static IEnumerable<string> UnwrittenProperties(Type type, JsonElement element)
    {
        var naming = WorkflowGraph.SerializationOptions.PropertyNamingPolicy
            ?? throw new InvalidOperationException(
                "el formato del flujo escribe con una política de nombres; sin ella el archivo no se puede " +
                "comprobar campo a campo");

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            string name = naming.ConvertName(property.Name);

            if (!element.TryGetProperty(name, out _))
            {
                yield return $"{type.Name}.{property.Name} (no aparece como '{name}')";
            }
        }
    }
}
