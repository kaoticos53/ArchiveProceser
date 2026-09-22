using System;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.App.Services;

/// <summary>
/// Sincroniza reactivamente la consola de logs con el inspector de nodos: al cambiar la fila
/// seleccionada se localiza el nodo asociado y se pueblan sus salidas, metadatos y diferenciales.
/// Centraliza la suscripción a <see cref="LogViewModel.LogSelectionChanged"/> para garantizar
/// exactamente un suscriptor por par consola-inspector.
/// </summary>
public sealed class LogInspectorSyncService : IDisposable
{
    private readonly LogViewModel _logConsole;
    private readonly NodeInspectorViewModel _inspector;
    private bool _disposed;

    public LogInspectorSyncService(LogViewModel logConsole, NodeInspectorViewModel inspector)
    {
        _logConsole = logConsole;
        _inspector = inspector;
        _logConsole.LogSelectionChanged += OnLogSelectionChanged;
    }

    private void OnLogSelectionChanged(StructuredLogRecord? log)
    {
        if (log != null)
        {
            _inspector.InspectLogRecord(log);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logConsole.LogSelectionChanged -= OnLogSelectionChanged;
    }
}
