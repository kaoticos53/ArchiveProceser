using System.Collections.Concurrent;
using System.IO;
using System.Threading.Channels;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Servicio de supervisión de carpetas en tiempo real (Watch Folder) con mecanismo de debounce
/// anti-colisión para garantizar que los archivos hayan finalizado su escritura en disco antes de procesarlos.
/// Soporta múltiples carpetas de origen simultáneas.
/// </summary>
public class FolderWatcherService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, DateTime> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<FileItemContext> _itemChannel = Channel.CreateUnbounded<FileItemContext>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private readonly Lock _lock = new();

    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return _watchers.Count > 0 && _watchers.Any(w => w.EnableRaisingEvents);
            }
        }
    }

    public ChannelReader<FileItemContext> ItemReader => _itemChannel.Reader;
    public event Action<FileItemContext>? ItemDiscovered;

    public void Start(string folderPath, string filter = "*.*", bool includeSubdirectories = true, int debounceMs = 1000)
    {
        Start([folderPath], filter, includeSubdirectories, debounceMs);
    }

    public void Start(IEnumerable<string> folderPaths, string filter = "*.*", bool includeSubdirectories = true, int debounceMs = 1000)
    {
        Stop();

        lock (_lock)
        {
            _cts = new CancellationTokenSource();

            foreach (var rawPath in folderPaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath)) continue;
                string expandedPath = Environment.ExpandEnvironmentVariables(rawPath);

                if (!Directory.Exists(expandedPath))
                {
                    try
                    {
                        Directory.CreateDirectory(expandedPath);
                    }
                    catch
                    {
                        continue;
                    }
                }

                var watcher = new FileSystemWatcher(expandedPath, filter)
                {
                    IncludeSubdirectories = includeSubdirectories,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Created += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Renamed += OnRenamedEvent;
                watcher.EnableRaisingEvents = true;

                _watchers.Add(watcher);
            }

            if (_watchers.Count > 0)
            {
                _processingTask = Task.Run(() => ProcessPendingQueueAsync(debounceMs, _cts.Token));
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnFileSystemEvent;
                    watcher.Changed -= OnFileSystemEvent;
                    watcher.Renamed -= OnRenamedEvent;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();

            if (_cts != null)
            {
                _cts.Cancel();
                try
                {
                    _processingTask?.Wait(1000);
                }
                catch { }

                _cts.Dispose();
                _cts = null;
                _processingTask = null;
            }

            _pendingFiles.Clear();
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (File.Exists(e.FullPath))
        {
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
        }
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e)
    {
        if (File.Exists(e.FullPath))
        {
            _pendingFiles.TryRemove(e.OldFullPath, out _);
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
        }
    }

    private async Task ProcessPendingQueueAsync(int debounceMs, CancellationToken cancellationToken)
    {
        int pollInterval = Math.Min(100, Math.Max(25, debounceMs / 4));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, cancellationToken);
                var now = DateTime.UtcNow;

                foreach (var (filePath, lastEventTime) in _pendingFiles.ToArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if ((now - lastEventTime).TotalMilliseconds >= debounceMs)
                    {
                        if (File.Exists(filePath) && IsFileLockedAndReady(filePath))
                        {
                            if (_pendingFiles.TryRemove(filePath, out _))
                            {
                                var item = new FileItemContext(filePath)
                                {
                                    OriginalPath = filePath,
                                    FileSizeBytes = new FileInfo(filePath).Length
                                };
                                item.Metadata["WatchFolderEvent"] = "CreatedOrChanged";
                                item.Metadata["DetectedAt"] = DateTime.UtcNow.ToString("o");
                                item.AddLog($"FolderWatcherService: Archivo detectado tras debounce ({filePath})");

                                ItemDiscovered?.Invoke(item);
                                await _itemChannel.Writer.WriteAsync(item, cancellationToken);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignorar excepciones transitorias de polling
            }
        }
    }

    private static bool IsFileLockedAndReady(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return stream.Length >= 0;
        }
        catch (IOException)
        {
            // El archivo sigue bloqueado por otro proceso en escritura
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
