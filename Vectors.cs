namespace Samicpp.Http.Vectors;

using System.Collections;
using System.Runtime.InteropServices;

public unsafe class Vector<T> : IDisposable, IEnumerable<T> //, ICollection<T>
where T: unmanaged
{
    T* ptr;
    nuint len;
    bool growable = false;

    // public int Count { get => (int)len; }
    public nuint Length { get => len; }
    public bool Growable { get => growable; init => growable = value; }
    // public bool IsReadOnly { get; } = false;

    public Vector() : this(0, true) {}
    public Vector(long length, bool expandable = false) : this((nuint)length, expandable) {}
    public Vector(nuint length, bool expandable = false)
    {
        void* res = NativeMemory.Alloc(length * (nuint)sizeof(T));
        
        if (res == null)
        {
            throw new NullReferenceException("memory allocation failed");
        }

        len = length;
        ptr = (T*)res;
        growable = expandable;
    }
    public Vector(T[] initial) : this(initial.Length)
    {
        fixed (T* arr = initial) NativeMemory.Copy(arr, ptr, (nuint)initial.Length * (nuint)sizeof(T));
    }
    
    ~Vector() => Dispose();
    public void Dispose()
    {
        if (ptr != null) NativeMemory.Free(ptr);
        ptr = null;
        len = 0;
        growable = false;
        GC.SuppressFinalize(this);
    }

    // public T? Get(nuint index) => index >= len ? null : ptr[index];
    // public unsafe T GetUnchecked(nuint index) => ptr[index];
    public bool TryGetValue(nuint index, out T res)
    {
        if (index < len)
        {
            res = ptr[index];
            return true;
        }

        res = default;
        return false;
    }

    public T this[nuint i]
    {
        get =>     i >= len ? throw new IndexOutOfRangeException($"{i} out of bounds") : ptr[i];
        set => _ = i >= len ? throw new IndexOutOfRangeException($"{i} out of bounds") : ptr[i] = value;
    }
    public T this[long i]
    {
        get =>     (ulong)i >= len || 0 > i ? throw new IndexOutOfRangeException($"{i} out of bounds") : ptr[(nuint)i];
        set => _ = (ulong)i >= len || 0 > i ? throw new IndexOutOfRangeException($"{i} out of bounds") : ptr[(nuint)i] = value;
    }
    public Span<T> this[Range range]
    {
        get
        {
            nint start = range.Start.IsFromEnd ? (nint)len - range.Start.Value : range.Start.Value;
            nint end = range.End.IsFromEnd ? (nint)len - range.End.Value : range.End.Value;
            nint offset = end - start;

            return AsSpan((nuint)start, (nuint)offset);
        }
    }
    public T this[Index index]
    {
        get
        {
            nint pos = index.IsFromEnd ? (nint)len - index.Value : index.Value;
            return this[(nuint)pos];
        }
    }

    void Resize(nuint length)
    {
        void* nptr = NativeMemory.Realloc(ptr, length * (nuint)sizeof(T));

        if (nptr == null)
        {
            throw new InsufficientMemoryException("memory allocation failed");
            // throw new OutOfMemoryException("memory allocation failed");
        }

        // if (length < len) NativeMemory.Copy(ptr, nptr, length * (nuint)sizeof(T));
        // else NativeMemory.Copy(ptr, nptr, len * (nuint)sizeof(T));
        // NativeMemory.Free(ptr);

        len = length;
        ptr = (T*)nptr;
    }
    public void Expand(nint size)
    {
        if (!growable) throw new NotSupportedException("array is not growable");
        // nint nsize = (nint)len + size;
        // Resize((nuint)nsize);
        nuint fsize = (nuint)nint.Abs(size);

        if (size < 0 && fsize > len) Resize(0);
        else if (size < 0) Resize(len - fsize);
        else Resize(len + fsize);
    }
    public void Add(T item)
    {
        if (!growable) throw new NotSupportedException("array is not growable");
        Resize(len + 1);
        ptr[len - 1] = item;
    }
    public void Add(params T[] items) => Add(items.AsSpan());
    public void Add(Span<T> span)
    {
        if (!growable) throw new NotSupportedException("array is not growable");
        nuint olen = len;
        Resize(len + (nuint)span.Length);
        Span<T> dspan = AsSpan(olen, len - olen);
        span.CopyTo(dspan);
    }

    public void Clear(nuint offset = 0)
    {
        NativeMemory.Clear(ptr + offset, (len - offset) * (nuint)sizeof(T));
    }

    public T[] ToArray()
    {
        if (len > int.MaxValue) throw new OverflowException("length too big");
        
        T[] arr = new T[len];
        fixed (T* ar = arr) NativeMemory.Copy(ptr, ar, len * (nuint)sizeof(T));
        return arr;
    }
    public Span<T> AsSpan()
    {
        if (len > int.MaxValue) throw new OverflowException("length too big");
        return new(ptr, (int)len);
    }
    public Span<T> AsSpan(nuint offset, nuint length)
    {
        // if (offset > length) throw new ArgumentException("offset bigger than length");
        if (offset > len || length > len - offset) throw new IndexOutOfRangeException("");
        if (length > int.MaxValue) throw new OverflowException("length too big");
        return new(ptr + offset, (int)length);
    }

    public IEnumerator<T> GetEnumerator() => new Enumerator(ptr, len);
    public IEnumerator<T> GetEnumerator(nuint offset, nuint length) => new Enumerator(ptr + offset, length);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(ptr, len);
    struct Enumerator(T* ptr, nuint len) : IEnumerator<T>
    {
        readonly T* ptr = ptr;
        readonly nuint len = len;
        nint index = -1;

        public readonly T Current => ptr[index];
        object IEnumerator.Current => Current;

        public bool MoveNext() => (nuint)(++index) < len;
        public void Reset() => index = -1;
        public readonly void Dispose() { }
    }


}