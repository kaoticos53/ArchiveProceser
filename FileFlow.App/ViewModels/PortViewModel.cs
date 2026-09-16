using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Serialization;
using Material.Icons;

namespace FileFlow.App.ViewModels;

/// <summary>Forma geométrica del socket, derivada del tipo de dato que transporta.</summary>
public enum PortSocketShape
{
    /// <summary>Texto y rutas.</summary>
    Circle,

    /// <summary>Flujo de archivos y colecciones.</summary>
    Square,

    /// <summary>Booleanos y condiciones.</summary>
    Triangle,

    /// <summary>Valores numéricos.</summary>
    Diamond
}

/// <summary>
/// Familia semántica del dato. Determina el color (por token del tema, vía clases de estilo), la forma
/// del socket y si dos puertos son compatibles entre sí.
/// </summary>
public enum PortTypeKind
{
    Files,
    Text,
    Boolean,
    Number,
    Binary,
    Collection,
    Any
}

public partial class PortViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private PortDirection _direction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SocketShape))]
    [NotifyPropertyChangedFor(nameof(TypeKind))]
    [NotifyPropertyChangedFor(nameof(IsCircleSocket))]
    [NotifyPropertyChangedFor(nameof(IsSquareSocket))]
    [NotifyPropertyChangedFor(nameof(IsTriangleSocket))]
    [NotifyPropertyChangedFor(nameof(IsDiamondSocket))]
    [NotifyPropertyChangedFor(nameof(IsFilesType))]
    [NotifyPropertyChangedFor(nameof(IsTextType))]
    [NotifyPropertyChangedFor(nameof(IsBooleanType))]
    [NotifyPropertyChangedFor(nameof(IsNumberType))]
    [NotifyPropertyChangedFor(nameof(IsBinaryType))]
    [NotifyPropertyChangedFor(nameof(IsCollectionType))]
    [NotifyPropertyChangedFor(nameof(IsAnyType))]
    [NotifyPropertyChangedFor(nameof(DataTypeSimpleName))]
    [NotifyPropertyChangedFor(nameof(DataTypeDescription))]
    private Type _dataType = typeof(FileItemContext);

    [ObservableProperty]
    private Point _anchor;

    [ObservableProperty]
    private int _transmittedCount;

    [ObservableProperty]
    private string _lastItemInfoText = LocalizationManager.Instance.GetString("Port_Item_None", "Ninguno");

    [ObservableProperty]
    private ObservableCollection<KeyValuePair<string, string>> _metadataVariables = new();

    [ObservableProperty]
    private bool _hasMetadataVariables;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStateClasses))]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(SocketToolTip))]
    private bool _isConnected;

    /// <summary>Resumen de los nodos conectados, para el texto de estado (se compone localizado al vuelo).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(SocketToolTip))]
    private string _connectedTargetsSummary = string.Empty;

    [ObservableProperty]
    private MaterialIconKind _connectionStatusIcon = MaterialIconKind.CircleOutline;

    // ─────────────────────────────────────────────────────────────────────────────
    // Estado de arrastre: resaltado de puertos compatibles mientras se dibuja un cable
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Este puerto es el origen del cable que se está arrastrando.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDimmedDuringDrag))]
    [NotifyPropertyChangedFor(nameof(DragStateClasses))]
    private bool _isDragSource;

    /// <summary>El puerto admite la conexión (dirección y nodo válidos).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDimmedDuringDrag))]
    [NotifyPropertyChangedFor(nameof(DragStateClasses))]
    private bool _isConnectableDuringDrag;

    /// <summary>Además de conectable, los tipos de dato encajan sin conversión.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DragStateClasses))]
    private bool _isTypeCompatibleDuringDrag;

    /// <summary>Hay un arrastre en curso en el lienzo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDimmedDuringDrag))]
    private bool _isDragActive;

    /// <summary>Los puertos no compatibles se atenúan para que la vista se centre en los válidos.</summary>
    public bool IsDimmedDuringDrag => IsDragActive && !IsDragSource && !IsConnectableDuringDrag;

    /// <summary>
    /// El puerto admite la conexión pero los tipos de dato no encajan sin conversión: se resalta en ámbar
    /// para avisar sin bloquear (el motor valida los tipos en tiempo de ejecución).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DragStateClasses))]
    private bool _isTypeWarningDuringDrag;

    /// <summary>Clases de estado del arrastre: compatible · advertencia (tipo distinto) · atenuado.</summary>
    public string DragStateClasses =>
        IsDragSource ? "dragSource"
        : IsConnectableDuringDrag ? (IsTypeCompatibleDuringDrag ? "compatible" : "compatibleWarning")
        : "incompatible";

    /// <summary>Clases de estado de conexión, para que el tema decida el color del socket.</summary>
    public string ConnectionStateClasses => IsConnected ? "connected" : "free";

    public NodeViewModel NodeOwner { get; }

    public PortSocketShape SocketShape => GetShapeForDataType(DataType);

    public PortTypeKind TypeKind => GetTypeKind(DataType);

    public bool IsCircleSocket => SocketShape == PortSocketShape.Circle;
    public bool IsSquareSocket => SocketShape == PortSocketShape.Square;
    public bool IsTriangleSocket => SocketShape == PortSocketShape.Triangle;
    public bool IsDiamondSocket => SocketShape == PortSocketShape.Diamond;

    public bool IsFilesType => TypeKind == PortTypeKind.Files;
    public bool IsTextType => TypeKind == PortTypeKind.Text;
    public bool IsBooleanType => TypeKind == PortTypeKind.Boolean;
    public bool IsNumberType => TypeKind == PortTypeKind.Number;
    public bool IsBinaryType => TypeKind == PortTypeKind.Binary;
    public bool IsCollectionType => TypeKind == PortTypeKind.Collection;
    public bool IsAnyType => TypeKind == PortTypeKind.Any;

    /// <summary>Icono vectorial de la dirección del puerto (la etiqueta textual se compone aparte).</summary>
    public MaterialIconKind DirectionIcon => Direction == PortDirection.Input
        ? MaterialIconKind.Import
        : MaterialIconKind.Export;

    public string DataTypeSimpleName => DataType == typeof(FileItemContext) ? "FileContext" : DataType.Name;

    /// <summary>
    /// Estado de conexión del puerto. Se compone a partir del estado y no se cachea, para que el cambio de
    /// idioma en caliente se refleje sin reiniciar nada (el nodo pide el refresco en <c>OnLanguageChanged</c>).
    /// </summary>
    public string ConnectionStatusText => IsConnected
        ? (string.IsNullOrWhiteSpace(ConnectedTargetsSummary)
            ? LocalizationManager.Instance.GetString("Port_Status_Connected", "Conectado")
            : LocalizationManager.Instance.GetFormattedString("Port_Status_ConnectedTo", "Conectado a {0}", ConnectedTargetsSummary))
        : LocalizationManager.Instance.GetString("Port_Status_Free", "Puerto libre (sin conexión)");

    public string TransmittedCountText => TransmittedCount == 1
        ? LocalizationManager.Instance.GetString("Port_ItemCount_One", "1 elemento")
        : LocalizationManager.Instance.GetFormattedString("Port_ItemCount_Many", "{0} elementos", TransmittedCount);

    /// <summary>
    /// Tooltip del socket: nombre, dirección, familia de tipo, tipo real y estado. Es la forma de descubrir
    /// la semántica de la forma y el color sin tener que abrir el inspector.
    /// </summary>
    public string SocketToolTip =>
        $"{DisplayName} · {DirectionLabel} · {GetTypeKindLabel(TypeKind)} ({DataTypeSimpleName})\n{Description}\n{ConnectionStatusText}";

    public PortViewModel(NodeViewModel owner, string name, string displayName, PortDirection direction, Type dataType, string description = "")
    {
        NodeOwner = owner;
        _name = name;
        _displayName = displayName;
        _direction = direction;
        _dataType = dataType;
        _description = string.IsNullOrWhiteSpace(description) ? GetDescriptionForDataType(dataType) : description;
    }

    public string DataTypeDescription => Description;

    public void UpdatePortContext(FileItemContext item)
    {
        TransmittedCount++;
        string fileName = Path.GetFileName(item.CurrentPath);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = item.CurrentPath;

        string size = item.Metadata.TryGetValue("FileSizeFormatted", out var sz) && sz != null ? sz.ToString() ?? "" : "";
        LastItemInfoText = string.IsNullOrWhiteSpace(size) ? fileName : $"{fileName} ({size})";

        MetadataVariables.Clear();
        foreach (var kvp in item.Metadata)
        {
            if (kvp.Value != null)
            {
                MetadataVariables.Add(new KeyValuePair<string, string>(kvp.Key, JsonDefaults.UnescapeUnicode(kvp.Value.ToString()) ?? ""));
            }
        }

        HasMetadataVariables = MetadataVariables.Count > 0;
        OnPropertyChanged(nameof(TransmittedCountText));
    }

    public void UpdateConnectionState(bool isConnected, string targetNodesSummary = "")
    {
        IsConnected = isConnected;
        ConnectedTargetsSummary = targetNodesSummary;
        ConnectionStatusIcon = isConnected ? MaterialIconKind.CheckboxMarkedCircle : MaterialIconKind.CircleOutline;
    }

    /// <summary>Recompone los textos localizados del puerto tras un cambio de idioma en caliente.</summary>
    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(TransmittedCountText));
        OnPropertyChanged(nameof(DataTypeDescription));
        OnPropertyChanged(nameof(DirectionLabel));
        OnPropertyChanged(nameof(SocketToolTip));
    }

    /// <summary>
    /// Evalúa este puerto como posible destino del cable que se está arrastrando desde <paramref name="source"/>.
    /// Distingue tres situaciones: origen, destino válido con tipos compatibles, destino válido con aviso de
    /// tipo y destino inválido (se atenúa).
    /// </summary>
    public void ApplyDragHighlight(PortViewModel? source)
    {
        IsDragSource = source is not null && ReferenceEquals(this, source);
        bool connectable = source is not null && !IsDragSource && CanConnect(source, this);

        IsConnectableDuringDrag = connectable;
        IsTypeCompatibleDuringDrag = connectable && AreTypesCompatible(source!, this);
        IsTypeWarningDuringDrag = IsConnectableDuringDrag && !IsTypeCompatibleDuringDrag;
    }

    /// <summary>Devuelve el puerto al estado de reposo cuando termina el arrastre.</summary>
    public void ClearDragHighlight()
    {
        IsDragActive = false;
        IsDragSource = false;
        IsConnectableDuringDrag = false;
        IsTypeCompatibleDuringDrag = false;
        IsTypeWarningDuringDrag = false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Compatibilidad
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ¿Se pueden unir estos dos puertos? Reglas estructurales: puertos distintos, nodos distintos y
    /// direcciones opuestas (el motor no puede encadenar dos salidas ni dos entradas).
    /// </summary>
    public static bool CanConnect(PortViewModel a, PortViewModel b)
        => a is not null
           && b is not null
           && !ReferenceEquals(a, b)
           && !ReferenceEquals(a.NodeOwner, b.NodeOwner)
           && a.Direction != b.Direction;

    /// <summary>
    /// ¿Encajan los tipos de dato sin conversión? Un puerto <c>object</c> acepta cualquier cosa y
    /// <c>FileItemContext</c> sólo admite contexto de archivo. Es una comprobación **orientativa**: el
    /// puerto de entrada debe poder recibir el tipo que emite la salida.
    /// </summary>
    public static bool AreTypesCompatible(PortViewModel a, PortViewModel b)
    {
        PortViewModel output = a.Direction == PortDirection.Output ? a : b;
        PortViewModel input = a.Direction == PortDirection.Output ? b : a;

        return IsAssignable(output.DataType, input.DataType);
    }

    public static bool IsAssignable(Type from, Type to)
    {
        if (from == to) return true;
        if (to == typeof(object) || from == typeof(object)) return true;

        return to.IsAssignableFrom(from);
    }

    /// <summary>Clasifica el tipo de dato en su familia semántica.</summary>
    public static PortTypeKind GetTypeKind(Type type)
    {
        if (type == typeof(FileItemContext)) return PortTypeKind.Files;
        if (type == typeof(string)) return PortTypeKind.Text;
        if (type == typeof(bool)) return PortTypeKind.Boolean;
        if (type == typeof(byte[]) || typeof(Stream).IsAssignableFrom(type)) return PortTypeKind.Binary;
        if (type == typeof(object)) return PortTypeKind.Any;
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string)) return PortTypeKind.Collection;
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return PortTypeKind.Number;
        if (type.IsEnum) return PortTypeKind.Number;

        return PortTypeKind.Any;
    }

    public static PortSocketShape GetShapeForDataType(Type type) => GetTypeKind(type) switch
    {
        PortTypeKind.Boolean => PortSocketShape.Triangle,
        PortTypeKind.Number => PortSocketShape.Diamond,
        PortTypeKind.Text => PortSocketShape.Circle,
        _ => PortSocketShape.Square
    };

    public static string GetDescriptionForDataType(Type type)
    {
        if (type == typeof(FileItemContext)) return LocalizationManager.Instance.GetString("Port_Desc_FileContext", "Recibe o emite contexto completo de archivo con metadatos.");
        if (type == typeof(string)) return LocalizationManager.Instance.GetString("Port_Desc_Text", "Cadena de texto o ruta de archivo.");
        if (type == typeof(bool)) return LocalizationManager.Instance.GetString("Port_Desc_Boolean", "Valor condicional booleano (Verdadero / Falso).");
        if (type == typeof(int) || type == typeof(long)) return LocalizationManager.Instance.GetString("Port_Desc_Integer", "Valor numérico entero.");
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return LocalizationManager.Instance.GetString("Port_Desc_Decimal", "Valor numérico decimal.");
        if (type == typeof(byte[]) || typeof(Stream).IsAssignableFrom(type)) return LocalizationManager.Instance.GetString("Port_Desc_Binary", "Flujo de datos binarios o stream en memoria.");
        if (type == typeof(object)) return LocalizationManager.Instance.GetString("Port_Desc_Any", "Universal (Acepta cualquier tipo de dato).");
        return type.Name;
    }

    /// <summary>Etiqueta legible de la familia de tipo (para el inspector y los tooltips), localizada.</summary>
    public static string GetTypeKindLabel(PortTypeKind kind) => kind switch
    {
        PortTypeKind.Files => LocalizationManager.Instance.GetString("Port_Type_Files", "Archivo"),
        PortTypeKind.Text => LocalizationManager.Instance.GetString("Port_Type_Text", "Texto"),
        PortTypeKind.Boolean => LocalizationManager.Instance.GetString("Port_Type_Boolean", "Booleano"),
        PortTypeKind.Number => LocalizationManager.Instance.GetString("Port_Type_Number", "Numérico"),
        PortTypeKind.Binary => LocalizationManager.Instance.GetString("Port_Type_Binary", "Binario"),
        PortTypeKind.Collection => LocalizationManager.Instance.GetString("Port_Type_Collection", "Colección"),
        _ => LocalizationManager.Instance.GetString("Port_Type_Any", "Universal")
    };

    /// <summary>Etiqueta de la dirección del puerto (entrada/salida), localizada.</summary>
    public string DirectionLabel => Direction == PortDirection.Input
        ? LocalizationManager.Instance.GetString("Port_Direction_Input", "Entrada")
        : LocalizationManager.Instance.GetString("Port_Direction_Output", "Salida");
}
