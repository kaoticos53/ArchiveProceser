using System.Text.Json;
using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// Modelo de datos serializable para almacenar nodos en el portapapeles.
/// </summary>
public sealed class NodeClipboardItem
{
    public string OriginalId { get; set; } = string.Empty;
    public string NodeTypeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public bool IsExpanded { get; set; }
    public bool HasBreakpoint { get; set; }
    public bool IsLoggingEnabled { get; set; } = true;
    public string? AccentColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? InnerGraphJson { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Modelo de datos serializable para almacenar conexiones internas entre los nodos copiados.
/// </summary>
public sealed class EdgeClipboardItem
{
    public string SourceNodeOriginalId { get; set; } = string.Empty;
    public string SourcePortName { get; set; } = string.Empty;
    public string TargetNodeOriginalId { get; set; } = string.Empty;
    public string TargetPortName { get; set; } = string.Empty;
}

/// <summary>
/// Paquete contenedor del portapapeles con metadatos de versión y compatibilidad.
/// </summary>
public sealed class NodeClipboardPackage
{
    public string Schema { get; set; } = "FileFlow.NodeClipboard.v1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<NodeClipboardItem> Nodes { get; set; } = [];
    public List<EdgeClipboardItem> Edges { get; set; } = [];
}

/// <summary>
/// Implementación thread-safe y tolerante a fallos del servicio de portapapeles para el lienzo DAG.
/// </summary>
public sealed class NodeClipboardService : INodeClipboardService
{
    private const string ClipboardHeaderPrefix = "<!-- FileFlow.NodeClipboard.v1 -->";
    private readonly PluginLoader _pluginLoader;
    private readonly System.Threading.Lock _lock = new();
    private NodeClipboardPackage? _inMemoryFallback;
    private int _pasteCounter = 0;

    public NodeClipboardService(PluginLoader? pluginLoader = null)
    {
        _pluginLoader = pluginLoader ?? new PluginLoader();
    }

    public void Copy(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var selectedNodes = nodes.Where(n => n != null).ToList();
        if (selectedNodes.Count == 0) return;

        var selectedIds = new HashSet<string>(selectedNodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
        var package = new NodeClipboardPackage();

        foreach (var node in selectedNodes)
        {
            var item = new NodeClipboardItem
            {
                OriginalId = node.Id,
                NodeTypeName = node.NodeTypeName,
                Title = node.Title,
                X = node.Location.X,
                Y = node.Location.Y,
                Width = node.Width,
                IsExpanded = node.IsExpanded,
                HasBreakpoint = node.HasBreakpoint,
                IsLoggingEnabled = node.IsLoggingEnabled,
                AccentColor = node.AccentColor,
                HeaderColor = node.HeaderColor,
                InnerGraphJson = node.InnerGraphJson
            };

            // 1. Extraer parámetros configurados en los ViewModels (interfaz gráfica)
            foreach (var param in node.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(param.Key))
                {
                    item.Parameters[param.Key] = param.Value;
                }
            }

            // 2. Extraer parámetros de la instancia de nodo (para claves internas como CasesJson/MethodSteps o cambios directos)
            lock (node.NodeInstance.Parameters)
            {
                foreach (var (k, v) in node.NodeInstance.Parameters)
                {
                    if (!string.IsNullOrWhiteSpace(k) && v != null)
                    {
                        item.Parameters[k] = v;
                    }
                }
            }

            package.Nodes.Add(item);
        }

        // 3. Capturar conexiones internas entre los nodos seleccionados
        if (connections != null)
        {
            foreach (var conn in connections)
            {
                if (conn.Source?.NodeOwner != null && conn.Target?.NodeOwner != null)
                {
                    string srcId = conn.Source.NodeOwner.Id;
                    string tgtId = conn.Target.NodeOwner.Id;

                    if (selectedIds.Contains(srcId) && selectedIds.Contains(tgtId))
                    {
                        package.Edges.Add(new EdgeClipboardItem
                        {
                            SourceNodeOriginalId = srcId,
                            SourcePortName = conn.Source.Name,
                            TargetNodeOriginalId = tgtId,
                            TargetPortName = conn.Target.Name
                        });
                    }
                }
            }
        }

        lock (_lock)
        {
            _inMemoryFallback = package;
            _pasteCounter = 0;
        }

        // 4. Escribir de forma segura en el portapapeles del sistema
        try
        {
            string json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = false });
            string clipboardPayload = $"{ClipboardHeaderPrefix}\n{json}";

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Clipboard.SetText(clipboardPayload);
                    break;
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeClipboardService] Warning: Could not write to OS Clipboard: {ex.Message}. Using in-memory fallback.");
        }
    }

    public bool CanPaste()
    {
        lock (_lock)
        {
            if (_inMemoryFallback != null && _inMemoryFallback.Nodes.Count > 0)
            {
                return true;
            }
        }

        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text) && text.Contains(ClipboardHeaderPrefix))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignorar bloqueo temporal del portapapeles
        }

        return false;
    }

    public List<NodeViewModel> Paste(EditorViewModel editor, Point? targetPosition = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        NodeClipboardPackage? package = GetCurrentPackage();
        if (package == null || package.Nodes.Count == 0)
        {
            return [];
        }

        lock (_lock)
        {
            _pasteCounter++;
        }

        double minX = package.Nodes.Min(n => n.X);
        double minY = package.Nodes.Min(n => n.Y);

        Dictionary<string, NodeViewModel> idMapping = new(StringComparer.OrdinalIgnoreCase);
        List<NodeViewModel> createdNodes = [];

        // Deseleccionar nodos previamente existentes
        foreach (var existingNode in editor.Nodes)
        {
            existingNode.IsSelected = false;
        }

        // 1. Instanciar y configurar los nodos
        foreach (var item in package.Nodes)
        {
            IFlowNode? instance = _pluginLoader.CreateNodeInstance(item.NodeTypeName);
            if (instance == null) continue;

            string newId = Guid.NewGuid().ToString();
            instance.Id = newId;

            // Transferir parámetros antes de construir el ViewModel para que la inicialización los tome directamente
            lock (instance.Parameters)
            {
                foreach (var (k, v) in item.Parameters)
                {
                    instance.Parameters[k] = UnwrapJsonValue(v);
                }
            }

            // Calcular posición de pegado
            Point newLocation;
            if (targetPosition.HasValue)
            {
                double offsetX = item.X - minX;
                double offsetY = item.Y - minY;
                newLocation = new Point(targetPosition.Value.X + offsetX, targetPosition.Value.Y + offsetY);
            }
            else
            {
                int currentCounter;
                lock (_lock)
                {
                    currentCounter = _pasteCounter;
                }
                double offset = 40.0 * currentCounter;
                newLocation = new Point(item.X + offset, item.Y + offset);
            }

            var nodeVm = new NodeViewModel(instance, newLocation)
            {
                ParentEditor = editor,
                Title = item.Title,
                HasBreakpoint = item.HasBreakpoint,
                IsLoggingEnabled = item.IsLoggingEnabled,
                Width = item.Width > 0 ? item.Width : 200,
                IsExpanded = item.IsExpanded,
                InnerGraphJson = item.InnerGraphJson ?? string.Empty,
                IsSelected = true
            };

            if (!string.IsNullOrWhiteSpace(item.AccentColor))
            {
                nodeVm.AccentColor = item.AccentColor;
            }
            if (!string.IsNullOrWhiteSpace(item.HeaderColor))
            {
                nodeVm.HeaderColor = item.HeaderColor;
            }

            // Sincronizar explícitamente los valores de los parámetros en los ViewModels
            foreach (var p in nodeVm.Parameters)
            {
                if (item.Parameters.TryGetValue(p.Key, out var rawVal))
                {
                    p.Value = UnwrapJsonValue(rawVal);
                }
            }

            // Si el nodo contiene parámetros dinámicos (ej. VariableInjectorNode) que no están en descriptores, asegurarse de que se reflejen
            if (nodeVm.IsVariableInjectorNode)
            {
                foreach (var (k, v) in item.Parameters)
                {
                    if (k.StartsWith("Var_", StringComparison.OrdinalIgnoreCase) && !nodeVm.Parameters.Any(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        var unwrapped = UnwrapJsonValue(v)?.ToString() ?? "";
                        nodeVm.Parameters.Add(new NodeParameterViewModel(new NodeParameterDescriptor(k, ParameterEditorType.Text, DefaultValue: unwrapped), unwrapped, nodeOwner: nodeVm));
                    }
                }
            }

            idMapping[item.OriginalId] = nodeVm;
            createdNodes.Add(nodeVm);
        }

        // 2. Registrar nodos en el editor
        foreach (var node in createdNodes)
        {
            editor.Nodes.Add(node);
            editor.BringToFront(node);
        }

        // 3. Recrear las aristas/conexiones internas con los nuevos IDs
        foreach (var edge in package.Edges)
        {
            if (idMapping.TryGetValue(edge.SourceNodeOriginalId, out var srcNode) &&
                idMapping.TryGetValue(edge.TargetNodeOriginalId, out var tgtNode))
            {
                var srcPort = srcNode.OutputPorts.FirstOrDefault(p => p.Name.Equals(edge.SourcePortName, StringComparison.OrdinalIgnoreCase));
                var tgtPort = tgtNode.InputPorts.FirstOrDefault(p => p.Name.Equals(edge.TargetPortName, StringComparison.OrdinalIgnoreCase));

                if (srcPort != null && tgtPort != null)
                {
                    editor.CreateConnection(srcPort, tgtPort);
                }
            }
        }

        return createdNodes;
    }

    public List<NodeViewModel> Duplicate(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections, EditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        Copy(nodes, connections);
        return Paste(editor, targetPosition: null);
    }

    private NodeClipboardPackage? GetCurrentPackage()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text) && text.Contains(ClipboardHeaderPrefix))
                {
                    int index = text.IndexOf(ClipboardHeaderPrefix, StringComparison.Ordinal);
                    string json = text[(index + ClipboardHeaderPrefix.Length)..].Trim();
                    var package = JsonSerializer.Deserialize<NodeClipboardPackage>(json);
                    if (package != null && package.Nodes.Count > 0)
                    {
                        return package;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeClipboardService] Could not read OS Clipboard: {ex.Message}. Falling back to memory.");
        }

        lock (_lock)
        {
            return _inMemoryFallback;
        }
    }

    private static object? UnwrapJsonValue(object? val)
    {
        if (val is JsonElement elem)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String => elem.GetString(),
                JsonValueKind.Number => elem.TryGetInt64(out long l) ? l : (elem.TryGetDouble(out double d) ? d : elem.GetRawText()),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => elem.GetRawText()
            };
        }
        return val;
    }
}
