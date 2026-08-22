using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

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
    private Type _dataType = typeof(FileItemContext);

    [ObservableProperty]
    private Point _anchor;

    [ObservableProperty]
    private int _transmittedCount;

    [ObservableProperty]
    private string _lastItemInfoText = "Ninguno";

    [ObservableProperty]
    private ObservableCollection<KeyValuePair<string, string>> _metadataVariables = new();

    [ObservableProperty]
    private bool _hasMetadataVariables;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Puerto libre (Sin conexión)";

    [ObservableProperty]
    private string _connectionStatusIcon = "⚪";

    public NodeViewModel NodeOwner { get; }

    public string PortColor => GetColorForDataType(DataType);

    public string DataTypeDescription => GetDescriptionForDataType(DataType);

    public string DirectionIcon => Direction == PortDirection.Input ? "📥 ENTRADA" : "📤 SALIDA";

    public string DataTypeSimpleName => DataType == typeof(FileItemContext) ? "FileContext" : DataType.Name;

    public string TransmittedCountText => TransmittedCount == 1 ? "1 elemento" : $"{TransmittedCount} elementos";

    public PortViewModel(NodeViewModel owner, string name, string displayName, PortDirection direction, Type dataType, string description = "")
    {
        NodeOwner = owner;
        _name = name;
        _displayName = displayName;
        _direction = direction;
        _dataType = dataType;
        _description = string.IsNullOrWhiteSpace(description) ? GetDescriptionForDataType(dataType) : description;
    }

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
                MetadataVariables.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString() ?? ""));
            }
        }

        HasMetadataVariables = MetadataVariables.Count > 0;
        OnPropertyChanged(nameof(TransmittedCountText));
    }

    public void UpdateConnectionState(bool isConnected, string targetNodesSummary = "")
    {
        IsConnected = isConnected;
        ConnectionStatusIcon = isConnected ? "🟢" : "⚪";
        ConnectionStatusText = isConnected 
            ? (string.IsNullOrWhiteSpace(targetNodesSummary) ? "Conectado" : $"Conectado a {targetNodesSummary}")
            : "Puerto libre (Sin conexión)";
    }

    public static string GetColorForDataType(Type type)
    {
        if (type == typeof(FileItemContext)) return "#10B981"; // Emerald Green: Flujo de Archivos
        if (type == typeof(string)) return "#06B6D4";          // Cyan: Rutas y Cadenas de Texto
        if (type == typeof(bool)) return "#F59E0B";            // Amber: Booleanos y Condiciones
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return "#818CF8";                                  // Indigo: Valores Numéricos
        if (type == typeof(byte[]) || typeof(System.IO.Stream).IsAssignableFrom(type))
            return "#EC4899";                                  // Fuchsia: Datos Binarios / Streams
        if (type == typeof(object)) return "#A855F7";          // Purple: Universal / Cualquier tipo
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            return "#F43F5E";                                  // Rose: Colecciones / Lotes
        return "#8B5CF6";                                      // Purple Default
    }

    public static string GetDescriptionForDataType(Type type)
    {
        if (type == typeof(FileItemContext)) return "Recibe o emite contexto completo de archivo con metadatos.";
        if (type == typeof(string)) return "Cadena de texto o ruta de archivo.";
        if (type == typeof(bool)) return "Valor condicional booleano (Verdadero / Falso).";
        if (type == typeof(int) || type == typeof(long)) return "Valor numérico entero.";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "Valor numérico decimal.";
        if (type == typeof(byte[]) || typeof(System.IO.Stream).IsAssignableFrom(type)) return "Flujo de datos binarios o stream en memoria.";
        if (type == typeof(object)) return "Universal (Acepta cualquier tipo de dato).";
        return type.Name;
    }
}
