using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FileFlow.App.Collections;

/// <summary>
/// Almacén de logs paginado por bloques de memoria continua (Data Virtualization) de alta capacidad y cero presión en LOH.
/// Permite almacenar cientos de miles o millones de entradas de log con acceso indexado O(1) e instanciación bajo demanda.
/// </summary>
/// <typeparam name="T">Tipo de elemento (ej. LogEntry).</typeparam>
public class PagedLogStore<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private const int ChunkSize = 2048; // 16 KB por chunk para tipos por referencia (muy por debajo de los 85 KB del LOH)
    private readonly Lock _lock = new();
    private readonly List<T[]> _chunks = [];
    private int _count;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Count
    {
        get
        {
            lock (_lock) return _count;
        }
    }

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            lock (_lock)
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} fuera de rango [0, {_count - 1}].");

                return _chunks[index / ChunkSize][index % ChunkSize];
            }
        }
        set
        {
            lock (_lock)
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} fuera de rango [0, {_count - 1}].");

                _chunks[index / ChunkSize][index % ChunkSize] = value;
            }
            NotifyReset();
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            EnsureCapacityForOne();
            _chunks[_count / ChunkSize][_count % ChunkSize] = item;
            _count++;
        }
        NotifyReset();
    }

    public void AddRange(IEnumerable<T> items)
    {
        bool addedAny = false;
        lock (_lock)
        {
            foreach (var item in items)
            {
                EnsureCapacityForOne();
                _chunks[_count / ChunkSize][_count % ChunkSize] = item;
                _count++;
                addedAny = true;
            }
        }

        if (addedAny)
        {
            NotifyReset();
        }
    }

    private void EnsureCapacityForOne()
    {
        if (_count % ChunkSize == 0 && _count / ChunkSize == _chunks.Count)
        {
            _chunks.Add(new T[ChunkSize]);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _chunks.Clear();
            _count = 0;
        }
        NotifyReset();
    }

    public bool Contains(T item)
    {
        lock (_lock)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _count; i++)
            {
                if (comparer.Equals(_chunks[i / ChunkSize][i % ChunkSize], item))
                    return true;
            }
            return false;
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (_lock)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + _count > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            for (int i = 0; i < _count; i++)
            {
                array[arrayIndex + i] = _chunks[i / ChunkSize][i % ChunkSize];
            }
        }
    }

    public int IndexOf(T item)
    {
        lock (_lock)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _count; i++)
            {
                if (comparer.Equals(_chunks[i / ChunkSize][i % ChunkSize], item))
                    return i;
            }
            return -1;
        }
    }

    public void Insert(int index, T item) => throw new NotSupportedException("Insert arbitrario no soportado en PagedLogStore.");
    public bool Remove(T item) => throw new NotSupportedException("Remove arbitrario no soportado en PagedLogStore.");
    public void RemoveAt(int index) => throw new NotSupportedException("RemoveAt arbitrario no soportado en PagedLogStore.");

    public T[] ToArray()
    {
        lock (_lock)
        {
            var result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                result[i] = _chunks[i / ChunkSize][i % ChunkSize];
            }
            return result;
        }
    }

    public List<T> ToList()
    {
        lock (_lock)
        {
            var list = new List<T>(_count);
            for (int i = 0; i < _count; i++)
            {
                list.Add(_chunks[i / ChunkSize][i % ChunkSize]);
            }
            return list;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        T[] snapshot = ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            yield return snapshot[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void NotifyReset()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
