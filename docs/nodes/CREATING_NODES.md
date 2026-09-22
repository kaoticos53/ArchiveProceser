# Guía para la Creación de Nodos Personalizados

Esta guía explica en detalle cómo desarrollar nuevos nodos de procesamiento para **FileFlow Studio** utilizando **C# 14** y **.NET 10**.

---

## 📋 Requisitos Previos

Para crear un nodo de procesamiento, tu proyecto solo necesita referenciar el paquete/proyecto **`FileFlow.Sdk`**. No requiere dependencias de UI ni de ninguna librería gráfica: el nodo describe qué hacer y el host decide cómo mostrarlo.

---

## 🛠 Pasos para Crear un Nodo

> [!TIP]
> **Usa `FlowNodeBase` como clase base.** Los 70 nodos de los 11 plugins del proyecto derivan de
> [`FlowNodeBase`](../../FileFlow.Sdk/FlowNodeBase.cs), que ya aporta `Id`, el diccionario `Parameters`,
> los puertos (`Inputs`/`Outputs` con `protected set`), `MaxConcurrency`, `OnWorkflowCompletedAsync` y los
> ayudantes `GetParameter<T>`, `SetParameter`, `EmitAsync`, `Log`, `GetLocalizedString` y
> `GetLocalizedFormat`. Heredar de la base y poblar puertos y parámetros en el constructor evita repetir
> ese boilerplate; implementar `IFlowNode` a mano sólo se justifica en dobles de prueba o en nodos que
> necesiten control total del contrato.
>
> **Lee los parámetros con `GetParameter<T>`.** Es equivalente a `ParameterHelper.GetInt32/GetDouble/GetBoolean/GetString` —ambos delegan en `ParameterValueConverter`—, así que entiende valores de la UI,
> `JsonElement` de un perfil guardado y números embebidos en texto (`"50%"` → 50, `"250ms"` → 250 en un
> destino entero), y nunca desborda en silencio: fuera de rango devuelve el valor por defecto. Para un
> valor que no sea escalar (una lista o su JSON) usa `GetParameter<object?>(clave, null)`.
>
> **Puertos dinámicos con un hook, no sobrescribiendo la propiedad.** Si los puertos dependen de los
> parámetros del usuario (un puerto por caso de un switch, nombres configurados de un subflujo), sobrescribe
> `BuildInputPorts()` / `BuildOutputPorts()` y deja `Inputs`/`Outputs` intactos: la propiedad se evalúa en
> cada lectura delegando en el hook, así que no hay caché que invalidar cuando la UI o la carga de un
> perfil escriben en `Parameters`. Los puertos fijos se siguen asignando en el constructor como siempre.
>
> **Y si tus puertos cambian, anúncialo.** El lienzo dibuja una proyección de tus puertos y las conexiones
> apuntan a esa proyección, así que el editor necesita saber cuándo cambia la topología. Llama a
> `NotifyPortsChanged()` allí donde cambien (al fijar los casos de un switch, al aplicar nombres nuevos a un
> subflujo, al releer una lista de puertos) y, si tu nodo **materializa** los puertos en lugar de calcularlos
> al leerlos, sobrescribe `RefreshPortTopology()` para rederivarlos de los parámetros antes de anunciar. La
> llamada es idempotente y silenciosa cuando nada cambió: la base sólo emite `PortsChanged` si el conjunto de
> nombres es distinto del último anunciado, de modo que el inspector puede escribir parámetros en cada
> pulsación de tecla sin reconstruir nada. Con ese aviso el editor reconstruye la tarjeta y **descarta los
> cables que apuntaban a un puerto que ya no existe**; sin él quedan aristas hacia puertos inexistentes que
> el motor ya no vuelve a trazar. Contrato completo en
> [`IPortTopologyNode`](../../FileFlow.Sdk/IPortTopologyNode.cs).
>
> **Esto lo vigila un test.** `NodeArchitectureGuardTests` barre los proyectos de la solución y falla
> si un nodo implementa `IFlowNode` directamente, redeclara un miembro heredado (sin `override`) o deriva
> puertos que no son fijos sin anunciarlo nunca, indicando fichero y línea. `NodeRuntimeCatalogGuardTests`
> comprueba además que el conjunto de nodos declarados en los plugins y el que el cargador descubre en
> runtime sean **exactamente** el mismo —sin huérfanos ni intrusos— y que cada nodo se pueda instanciar por
> su nombre con `CreateNodeInstance`. Los metadatos que se traducen sí se declaran como `override`. La
> equivalencia entre las dos rutas de lectura la fijan `ParameterValueConverterTests`, que además invocan el
> `GetParameter<T>` real sobre un nodo real, `FlowNodeBasePortsTests` fija el contrato de puertos y
> `PortTopologyContractTests` / `PortTopologyCanvasTests` fijan el anuncio y la reacción del lienzo.

### 1. Definir la Clase Derivando de `FlowNodeBase`

Crea una clase pública que derive de `FlowNodeBase` (o de `AiFlowNodeBase` en el plugin de IA) y decórala
con el atributo `[NodeDefinition]`. Un nodo **no** declara `Id`, `Parameters`, `Inputs`, `Outputs` ni
`ExecuteAsync`: la base los aporta y el nodo los especializa con `override`.

> Los fragmentos de esta guía son extractos del ejemplo completo y compilable:
> [`examples/SampleMultiPortNode.cs`](examples/SampleMultiPortNode.cs).

```csharp
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Examples;

[NodeDefinition("SampleMultiPortNode_Name", "General", "SampleMultiPortNode_Desc", PipelineRole.Filter,
    "ejemplo", "filtro", "tamano", "extension", "multipuerto", "sample", "filter")]
public sealed class SampleMultiPortNode : FlowNodeBase
{
    public override string Name =>
        LocalizationManager.Instance.GetString("SampleMultiPortNode_Name", "Filtro Multi-Puerto de Archivos");

    public override string Category => "General";

    public override string Description => LocalizationManager.Instance.GetString(
        "SampleMultiPortNode_Desc",
        "Clasifica los elementos recibidos por sus dos entradas según su tamaño y extensión hacia tres puertos de salida.");
}
```

No hay `Id`: lo genera la base. Tampoco hay `Parameters` ni `Inputs`/`Outputs`: se pueblan en el constructor
(pasos 2 y 3), y `ExecuteAsync` llega en el paso 4. Los metadatos traducibles sí se declaran con `override`,
con el texto por defecto que se verá mientras la clave no esté en los diccionarios de idioma.

---

### 2. Definir los Puertos en el Constructor

Los puertos son lo que conecta el nodo con el resto del flujo. Cada uno declara su nombre (el identificador
con el que se guardan las conexiones), el tipo de dato que transporta, la dirección y la etiqueta visible:

```csharp
public SampleMultiPortNode()
{
    Inputs =
    [
        new NodePort("MainInput", typeof(FileItemContext), PortDirection.Input, "Entrada Principal"),
        new NodePort("SecondaryInput", typeof(FileItemContext), PortDirection.Input, "Entrada Secundaria")
    ];

    Outputs =
    [
        new NodePort("Approved", typeof(FileItemContext), PortDirection.Output, "Aprobados"),
        new NodePort("Rejected", typeof(FileItemContext), PortDirection.Output, "Rechazados"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];
}
```

Las conexiones se guardan **por nombre de puerto**, así que renombrar uno en una versión posterior del nodo
deja huérfanos los flujos ya guardados: elige nombres estables.

#### Puertos dinámicos

Si los puertos dependen de la configuración del usuario —un puerto por caso, los nombres de un subflujo, los
puertos declarados de un script—, no los asignes en el constructor: devuélvelos desde el hook y **anuncia el
cambio**, porque es lo único que permite al lienzo reconstruir la tarjeta y descartar los cables que
apuntaban a un puerto que ya no existe.

```csharp
// GetConfiguredPorts() es un ayudante tuyo: lee el parámetro y devuelve los nombres declarados.
protected override IReadOnlyList<NodePort> BuildOutputPorts() =>
    GetConfiguredPorts().Select(name => new NodePort(name, typeof(FileItemContext), PortDirection.Output, name)).ToList();

public void SetConfiguredPorts(IEnumerable<string> names)
{
    Parameters["PortNames"] = string.Join(';', names);

    NotifyPortsChanged();
}
```

En el proyecto hay cinco nodos así —`SwitchCaseNode`, `SubflowNode`, los dos nodos frontera de subflujo y
`CustomScriptNode`— y son la mejor referencia cuando el tuyo se parezca a alguno. El contrato completo está
en [`IPortTopologyNode`](../../FileFlow.Sdk/IPortTopologyNode.cs): si tu nodo **materializa** los puertos en
lugar de calcularlos al leerlos, sobrescribe `RefreshPortTopology()` para rederivarlos antes de anunciar.

> **Por qué importa materializar bien.** Al cargar o pegar un grafo, las conexiones se recrean emparejando
> *nombres de puerto*, y el host materializa antes de emparejar. Si los puertos de tu nodo no se pueden
> rederivar de sus parámetros —como la frontera de un subflujo, que sale del subgrafo y no de un parámetro—,
> `RefreshPortTopology()` no basta: hay que materializarlos en todos los caminos que reconstruyen el grafo
> (`DynamicPortMaterializer` es el sitio donde eso ocurre). Un puerto que no existe cuando se empareja no da
> error: el cable se pierde en silencio.
>
> Y si tu nodo saca esos puertos de algo que puede no estar disponible al reabrir —como la frontera de un
> subflujo, que sale de otro archivo—, **recuérdalos contigo**: el archivo del flujo viaja solo, sin el
> resto del mundo, así que lo que no se guarde con el nodo no vuelve. El contenedor de subflujo lo hace
> con dos parámetros internos que expone por [`ISubflowNode.RememberedPorts`](../../FileFlow.Sdk/ISubflowNode.cs).

El archivo del flujo declara además con qué **versión** del formato está escrito
([`WorkflowFormat`](../../FileFlow.Core/Engine/WorkflowFormat.cs)). Esa marca es lo que distingue un archivo
antiguo —al que le faltan datos que entonces no se guardaban— de uno completo, y lo que permite repararlo
al leerlo aprovechando lo que sí conserva: en un archivo anterior, las aristas nombran los puertos que un
contenedor exponía, así que de ahí se recuperan antes de reconstruir las conexiones. La versión **no**
sustituye a guardar el estado de diseño con el nodo: eso sigue siendo responsabilidad de quien lo crea, y
añadir un formato nuevo obliga a escribir su reparación.

Y ese cable que no encuentra su puerto es también una **consecuencia de tu nodo**: si renombras un puerto
propio —o lo materializas con otro nombre según la configuración—, las conexiones que apuntaban a él dejan
de tener destino. Al abrir un flujo el editor ahora lo cuenta en la consola, con los dos extremos y el
motivo, en vez de dejar un flujo incompleto con aspecto de estar completo; pero quien puede evitar el aviso
es quien mantiene estables los nombres.

---

### 3. Definir los Parámetros Configurables

Los parámetros se exponen automáticamente en el inspector del nodo. Dale un valor por defecto a cada clave
en el constructor, junto a los puertos:

```csharp
Parameters["TargetExtension"] = ".zip";
Parameters["MaxFileSizeMB"] = 25;
Parameters["StrictValidation"] = true;
```

y declara su esquema en `ParameterDescriptors`: orden en el inspector, editor adecuado, límites y texto de
ayuda. Sin descriptores el nodo también funciona —la interfaz crea los campos a partir del diccionario—,
pero se muestran en el orden en que se escribieron y sin ninguna explicación.

```csharp
public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
[
    new("TargetExtension", ParameterEditorType.Text, DefaultValue: ".zip", DisplayOrder: 1,
        HelpText: "Extensión que se considera válida, con el punto incluido."),

    new("MaxFileSizeMB", ParameterEditorType.Number, DefaultValue: 25, DisplayOrder: 2,
        Min: 0, Max: 10240, Step: 1, HelpText: "Tamaño máximo admitido, en megabytes."),

    new("StrictValidation", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3,
        HelpText: "Si está activo, un archivo que exceda el tamaño máximo se rechaza aunque su extensión coincida.")
];
```

El tipo del descriptor (`ParameterEditorType`) decide el control (`Text`, `Number`, `Toggle`, `FilePath`,
`Dropdown`, `Slider`…), `DependsOnKey`/`DependsOnValues` ocultan un campo mientras no aplique, y
`GetParameter<T>` entiende el valor tal como llegue —de la interfaz o de un perfil guardado—.

---

### 4. Implementar el Método de Ejecución `ExecuteAsync`

El motor invoca `ExecuteAsync` una vez por cada elemento que entra al nodo, indicándole por qué puerto entró.
Aquí va la lógica; los ayudantes de la base evitan el resto del andamiaje: `GetParameter<T>` para leer,
`Log` para registrar, `EmitAsync` para emitir y `SetParameter`/`GetLocalizedString` cuando haga falta.

```csharp
public override async Task ExecuteAsync(
    string inputPortName,
    FileItemContext item,
    IFlowExecutionContext context,
    CancellationToken cancellationToken)
{
    string targetExtension = GetParameter("TargetExtension", ".zip");
    int maxFileSizeMb = GetParameter("MaxFileSizeMB", 25);
    bool strictValidation = GetParameter("StrictValidation", true);

    cancellationToken.ThrowIfCancellationRequested();

    bool extensionMatches = Path.GetExtension(item.CurrentPath).Equals(targetExtension, StringComparison.OrdinalIgnoreCase);
    bool sizeMatches = item.FileSizeBytes <= maxFileSizeMb * 1024L * 1024L;

    if (extensionMatches && (sizeMatches || !strictValidation))
    {
        item.Metadata["ProcessedBy"] = nameof(SampleMultiPortNode);
        item.Metadata["InputSourcePort"] = inputPortName;

        Log(context, $"[SampleMultiPort] '{item.CurrentPath}' aprobado.", LogLevel.Information, item);
        await EmitAsync(context, item, "Approved");
        return;
    }

    item.Metadata["RejectionReason"] = extensionMatches ? "excede el tamaño máximo" : "extensión no admitida";
    item.AddLog("SampleMultiPortNode clasificó el elemento como rechazado.");

    await EmitAsync(context, item, "Rejected");
}
```

Tres cosas que conviene tener presentes al escribir el tuyo:

- **Los errores esperables se emiten, no se lanzan.** Un elemento que no cumple los requisitos sale por un
  puerto de error con el motivo en `Metadata`: así el flujo decide qué hacer con él. La excepción se reserva
  para lo que no debería ocurrir nunca (el ejemplo la propaga tras registrar la cancelación).
- **`EmitAsync` puede llamarse varias veces** en la misma ejecución, una por cada puerto al que quieras
  enviar el elemento.
- **Un nodo acumulador** (el que junta todos los elementos antes de escribir un informe, por ejemplo)
  completa su trabajo en `OnWorkflowCompletedAsync`, no en `ExecuteAsync`.

---

## 📦 Despliegue del Nodo

Para que FileFlow Studio cargue tu nuevo nodo:

1. Compila tu proyecto como librería de clases (`.dll`).
2. Copia el archivo `.dll` resultante a la carpeta `Plugins/` del directorio ejecutable de la aplicación.
3. Al iniciar FileFlow Studio, el catálogo detectará e incorporará el nodo automáticamente.

Ver el código completo en: **[`examples/SampleMultiPortNode.cs`](examples/SampleMultiPortNode.cs)**, que es el
fichero del que salen todos los fragmentos de esta guía.
