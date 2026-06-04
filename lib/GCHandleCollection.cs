// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

public sealed class GCHandleCollection : List<GCHandle>, IDisposable
{
    public GCHandle AddObject(object obj)
    {
        var handle = GCHandle.Alloc(obj);
        Add(handle);
        return handle;
    }

    public void RemoveAndFree(GCHandle handle)
    {
        Remove(handle);
        if (handle.IsAllocated)
            handle.Free();
    }

    public void Transfer(GCHandleCollection other)
    {
        AddRange(other);
        other.Clear();
    }

    public void Dispose()
    {
        foreach (var handle in this)
        {
            if (handle.IsAllocated)
                handle.Free();
        }
        Clear();
    }
}