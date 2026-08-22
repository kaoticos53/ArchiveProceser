using System.Collections.Concurrent;
using System.Threading.Channels;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Servicio de supervisión de carpetas en tiempo real (Watch Folder) con mecanismo de debounce
/// anti-colisión para garantizar que los archivos hayan finalizado su escritura en disco antes de procesarlos.
/// </summary>
public class FolderWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, DateTime> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<FileItemContext> _itemChannel = Channel.CreateUnbounded<FileItemContext>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;
    public ChannelReader<FileItemContext> ItemReader => _itemChannel.Reader;

    public void Start(string folderPath, string filter = "*.*", bool includeSubdirectories = true, int debounceMs = 1000)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Watch Directory '{folderPath}' does not exist.");
        }

        Stop();

        _cts = new CancellationTokenSource();
        _watcher = new FileSystemWatcher(folderPath, filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Renamed += OnRenamedEvent;
        _watcher.EnableRaisingEvents = true;

        _processingTask = Task.Run(() => ProcessPendingQueueAsync(debounceMs, _cts.Token));
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Changed -= OnFileSystemEvent;
            _watcher.Renamed -= OnRenamedEvent;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _pendingFiles.Clear();
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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(250, cancellationToken);
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
                                var item = new FileItemContext(filePath);
                                item.Metadata["WatchFolderEvent"] = "CreatedOrChanged";
                                item.Metadata["DetectedAt"] = DateTime.UtcNow.ToString("o");
                                item.AddLog($"FolderWatcherService: File ready after debounce ({filePath})");

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
                // Ignore transient polling exceptions
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
            // File is locked by another process writing to it
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
