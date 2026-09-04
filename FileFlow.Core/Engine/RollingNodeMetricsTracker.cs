using System.Collections.Concurrent;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Engine;

/// <summary>
/// Acumulador concurrente de ventana deslizante (N=8 muestras) de alta velocidad para métricas granulares por nodo.
/// Calcula medias móviles de latencia, memoria RAM asignada por elemento, uso de CPU y aceleración por GPU sin asignaciones en el heap.
/// </summary>
public sealed class RollingNodeMetricsTracker
{
    public const int WindowSize = 8;

    private sealed class NodeBuffer
    {
        private readonly NodeExecutionSample[] _samples = new NodeExecutionSample[WindowSize];
        private readonly Lock _lock = new();
        private int _head = 0;
        private int _count = 0;
        private long _peakAllocatedBytes = 0;
        private bool _hasGpu = false;

        public void Add(in NodeExecutionSample sample)
        {
            lock (_lock)
            {
                _samples[_head] = sample;
                _head = (_head + 1) % WindowSize;
                if (_count < WindowSize)
                {
                    _count++;
                }

                if (sample.AllocatedBytes > _peakAllocatedBytes)
                {
                    _peakAllocatedBytes = sample.AllocatedBytes;
                }

                if (sample.GpuAccelerated)
                {
                    _hasGpu = true;
                }
            }
        }

        public (double avgDuration, long avgAllocatedBytes, long peakAllocatedBytes, double avgCpu, bool hasGpu, NodeExecutionSample[] recent) GetSnapshot()
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    return (0.0, 0, 0, 0.0, false, Array.Empty<NodeExecutionSample>());
                }

                double totalDuration = 0.0;
                long totalAllocated = 0;
                double totalCpu = 0.0;
                var list = new NodeExecutionSample[_count];

                // Extraer muestras en orden cronológico (de más antigua a más reciente)
                int startIdx = (_count < WindowSize) ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    int idx = (startIdx + i) % WindowSize;
                    var s = _samples[idx];
                    list[i] = s;
                    totalDuration += s.DurationMs;
                    totalAllocated += s.AllocatedBytes;
                    totalCpu += s.CpuPercentage;
                }

                double avgDuration = totalDuration / _count;
                long avgAllocated = totalAllocated / _count;
                double avgCpu = totalCpu / _count;

                return (avgDuration, avgAllocated, _peakAllocatedBytes, avgCpu, _hasGpu, list);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _count = 0;
                _peakAllocatedBytes = 0;
                _hasGpu = false;
                Array.Clear(_samples);
            }
        }
    }

    private readonly ConcurrentDictionary<string, NodeBuffer> _nodeBuffers = new(StringComparer.OrdinalIgnoreCase);

    public void RecordSample(string nodeId, double durationMs, long allocatedBytes, double cpuPercentage = 0.0, bool gpuAccelerated = false)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        var buffer = _nodeBuffers.GetOrAdd(nodeId, _ => new NodeBuffer());
        var sample = NodeExecutionSample.Create(durationMs, allocatedBytes, cpuPercentage, gpuAccelerated);
        buffer.Add(sample);
    }

    public (double avgDuration, long avgAllocatedBytes, long peakAllocatedBytes, double avgCpu, bool hasGpu, IReadOnlyList<NodeExecutionSample> recent) GetRollingMetrics(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !_nodeBuffers.TryGetValue(nodeId, out var buffer))
        {
            return (0.0, 0, 0, 0.0, false, Array.Empty<NodeExecutionSample>());
        }

        return buffer.GetSnapshot();
    }

    public void Reset(string? nodeId = null)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            if (_nodeBuffers.TryGetValue(nodeId, out var buffer))
            {
                buffer.Clear();
            }
        }
        else
        {
            foreach (var buffer in _nodeBuffers.Values)
            {
                buffer.Clear();
            }
            _nodeBuffers.Clear();
        }
    }
}
