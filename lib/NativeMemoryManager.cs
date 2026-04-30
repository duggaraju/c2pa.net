using System.Buffers;

public sealed unsafe class NativeMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    private readonly T* _ptr;
    private readonly int _length;

    public NativeMemoryManager(T* ptr, int length)
    {
        _ptr = ptr;
        _length = length;
    }

    public override Span<T> GetSpan() => new Span<T>(_ptr, _length);

    public override MemoryHandle Pin(int elementIndex = 0) => new MemoryHandle(_ptr + elementIndex);

    public override void Unpin() { }

    protected override void Dispose(bool disposing) { }
}