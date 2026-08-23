using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FileFlow.App.Collections;

/// <summary>
/// Búfer circular de tamaño fijo optimizado para rendimiento extremo y virtualización en WPF.
/// Implementa IList&lt;T&gt; e INotifyCollectionChanged con notificaciones en lote y O(1) de acceso indexado.
/// </summary>
/// <typeparam name="T">Tipo de elemento.</typeparam>
public class FastObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly Lock _lock = new();
    private T[] _buffer;
    private int _capacity;
    private int _head; // Índice del elemento más antiguo
    private int _count;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public FastObservableRingBuffer(int capacity = 10000)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad debe ser mayor que cero.");
        _capacity = capacity;
        _buffer = new T[capacity];
    }

    public int Capacity
    {
        get
        {
            lock (_lock) return _capacity;
        }
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "La capacidad debe ser mayor que cero.");
            lock (_lock)
            {
                if (_capacity == value) return;
                var currentItems = ToArrayInternal();
                _capacity = value;
                _buffer = new T[_capacity];
                _head = 0;
                _count = 0;

                int toCopy = Math.Min(currentItems.Length, _capacity);
                int startOffset = currentItems.Length - toCopy;
                for (int i = 0; i < toCopy; i++)
                {
                    _buffer[i] = currentItems[startOffset + i];
                }
                _count = toCopy;
            }
            NotifyReset();
        }
    }

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
                    throw new ArgumentOutOfRangeException(nameof(index));

                int actualIndex = (_head + index) % _capacity;
                return _buffer[actualIndex];
            }
        }
        set
        {
            lock (_lock)
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                int actualIndex = (_head + index) % _capacity;
                _buffer[actualIndex] = value;
            }
            NotifyReset();
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            AddInternal(item);
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
                AddInternal(item);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            NotifyReset();
        }
    }

    private void AddInternal(T item)
    {
        if (_count < _capacity)
        {
            int insertIndex = (_head + _count) % _capacity;
            _buffer[insertIndex] = item;
            _count++;
        }
        else
        {
            // Sobreescribir el más antiguo
            _buffer[_head] = item;
            _head = (_head + 1) % _capacity;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
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
                int actualIndex = (_head + i) % _capacity;
                if (comparer.Equals(_buffer[actualIndex], item))
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
                int actualIndex = (_head + i) % _capacity;
                array[arrayIndex + i] = _buffer[actualIndex];
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
                int actualIndex = (_head + i) % _capacity;
                if (comparer.Equals(_buffer[actualIndex], item))
                    return i;
            }
            return -1;
        }
    }

    public void Insert(int index, T item) => throw new NotSupportedException("Insert arbitrario no soportado en RingBuffer fijo.");
    public bool Remove(T item) => throw new NotSupportedException("Remove arbitrario no soportado en RingBuffer fijo.");
    public void RemoveAt(int index) => throw new NotSupportedException("RemoveAt arbitrario no soportado en RingBuffer fijo.");

    public T[] ToArray()
    {
        lock (_lock)
        {
            return ToArrayInternal();
        }
    }

    public List<T> ToList()
    {
        lock (_lock)
        {
            var list = new List<T>(_count);
            for (int i = 0; i < _count; i++)
            {
                int actualIndex = (_head + i) % _capacity;
                list.Add(_buffer[actualIndex]);
            }
            return list;
        }
    }

    private T[] ToArrayInternal()
    {
        var result = new T[_count];
        for (int i = 0; i < _count; i++)
        {
            int actualIndex = (_head + i) % _capacity;
            result[i] = _buffer[actualIndex];
        }
        return result;
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

    private void NotifyReset()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
