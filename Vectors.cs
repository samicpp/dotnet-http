namespace Samicpp.Http.Vectors;

using System.Collections;
using System.Runtime.InteropServices;

public unsafe class Vector<T> : IDisposable, IEnumerable<T>
where T: unmanaged
{
    T* ptr;
    nuint len;
    bool growable = false;

    public nuint Length { get => len; }
    public bool Growable { get => growable; }

    public Vector() : this(0, true) {}
    public Vector(long length, bool expandable = false) : this((nuint)length, expandable) {}
    public Vector(nuint length, bool expandable = false)
    {
        void* res = NativeMemory.Alloc(length * (nuint)sizeof(T));
        
        if ((nuint)res == 0)
        {
            throw new NullReferenceException("memory allocation failed");
        }

        len = length;
        ptr = (T*)res;
        growable = expandable;
    }
    
    ~Vector() => Dispose();
    public void Dispose()
    {
        NativeMemory.Free(ptr);
        ptr = null;
        len = 0;
        growable = false;
        GC.SuppressFinalize(this);
    }

    // public T? Get(nuint index) => index >= len ? null : ptr[index];
    // public unsafe T GetUnchecked(nuint index) => ptr[index];
    public bool TryGetValue(nuint index, out T? res)
    {
        if (index >= len)
        {
            res = ptr[index];
            return true;
        }

        res = null;
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

    public void Resize(nuint length)
    {
        if (!growable) throw new NotSupportedException("array is not growable");
        void* nptr = NativeMemory.Alloc(length * (nuint)sizeof(T));

        if ((nuint)nptr == 0)
        {
            throw new InsufficientMemoryException("memory allocation failed");
            // throw new OutOfMemoryException("memory allocation failed");
        }

        if (length < len) NativeMemory.Copy(ptr, nptr, length * (nuint)sizeof(T));
        else NativeMemory.Copy(ptr, nptr, len * (nuint)sizeof(T));

        NativeMemory.Free(ptr);

        len = length;
        ptr = (T*)nptr;
    }
    public void ExpandWith(Span<T> span)
    {
        nuint olen = len;
        Resize(len + (nuint)span.Length);
        
        for(int i = 0; i < span.Length; i++) ptr[olen + (nuint)i] = span[i];
    }
    public void Append(T item)
    {
        Resize(len + 1);
        ptr[len - 1] = item;
    }

    public void Clear()
    {
        NativeMemory.Clear(ptr, len * (nuint)sizeof(T));
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

    public IEnumerator<T> GetEnumerator() => new Enumerator(ptr, len);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(ptr, len);
    class Enumerator(T* ptr, nuint len) : IEnumerator<T>
    {
        T* ptr = ptr;
        nuint len = len;
        nint index = -1;

        public T Current => ptr[index];
        object IEnumerator.Current => Current;

        public bool MoveNext() => (nuint)(++index) < len;
        public void Reset() => index = 0;
        public void Dispose() { }
    }
}