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
    private PortDirection _direction;

    [ObservableProperty]
    private Type _dataType = typeof(FileItemContext);

    [ObservableProperty]
    private Point _anchor;

    public NodeViewModel NodeOwner { get; }

    public string PortColor => GetColorForDataType(DataType);

    public string DataTypeDescription => GetDescriptionForDataType(DataType);

    public PortViewModel(NodeViewModel owner, string name, string displayName, PortDirection direction, Type dataType)
    {
        NodeOwner = owner;
        _name = name;
        _displayName = displayName;
        _direction = direction;
        _dataType = dataType;
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
        if (type == typeof(FileItemContext)) return "Contexto de Archivo (FileItemContext)";
        if (type == typeof(string)) return "Texto / Ruta (String)";
        if (type == typeof(bool)) return "Booleano (True/False)";
        if (type == typeof(int) || type == typeof(long)) return "Entero (Integer)";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "Número Decimal (Float/Double)";
        if (type == typeof(byte[]) || typeof(System.IO.Stream).IsAssignableFrom(type)) return "Datos Binarios (Binary/Stream)";
        if (type == typeof(object)) return "Universal (Acepta cualquier tipo de dato)";
        return type.Name;
    }
}
