// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

/// <summary>
/// Managed wrapper around the native <see cref="C2paContextBuilder"/> object.
/// Use <see cref="Build"/> to produce an immutable <see cref="Context"/>.
/// </summary>
public sealed class ContextBuilder : IDisposable
{
    private unsafe C2paContextBuilder* builder;
    private GCHandleCollection handles = new();

    internal unsafe ContextBuilder(C2paContextBuilder* instance)
    {
        builder = instance;
    }

    /// <summary>
    /// Creates a new context builder with default settings.
    /// </summary>
    public ContextBuilder()
    {
        unsafe
        {
            var b = C2paBindings.context_builder_new();
            if (b == null)
                C2pa.CheckError();
            builder = b;
        }
    }

    public void SetSigner(ISigner signer)
    {
        SetSigner(Signer.From(signer));
    }


    /// <summary>
    /// Sets the settings on the context builder.
    /// </summary>
    public void SetSettings(C2pa.Settings settings)
    {
        SetSettings(settings.ToJson());
    }

    public void SetSettings(string settings, string format = "json")
    {
        using var handle = new C2paSettings();
        handle.Update(settings, format);
        SetSettings(handle);
    }

    /// <summary>
    /// Sets the settings on the context builder. The settings are cloned.
    /// </summary>
    public void SetSettings(C2paSettings settings)
    {
        EnsureNotBuilt();
        unsafe
        {
            var ret = C2paBindings.context_builder_set_settings(builder, settings.handle);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    /// <summary>
    /// Sets the signer on the context builder.
    /// </summary>
    public ContextBuilder SetSigner(Signer signer)
    {
        EnsureNotBuilt();
        GCHandleCollection signerHandles;
        unsafe
        {
            var ret = C2paBindings.context_builder_set_signer(builder, signer);
            signerHandles = signer.DetachHandles();
            if (ret == -1)
            {
                signerHandles.Dispose();
                C2pa.CheckError();
            }
        }
        handles.Transfer(signerHandles);
        return this;
    }

    /// <summary>
    /// Attaches a progress callback to the context builder.
    /// The callback receives the current phase along with the current and total
    /// progress values, and should return non-zero to continue or 0 to cancel.
    /// </summary>
    public ContextBuilder SetProgressCallback(Func<C2paProgressPhase, uint, uint, int> callback)
    {
        EnsureNotBuilt();

        var progressHandle = handles.AddObject(callback);

        unsafe
        {
            var ret = C2paBindings.context_builder_set_progress_callback(builder, (void*)(nint)progressHandle, &OnProgress);
            if (ret == -1)
            {
                handles.RemoveAndFree(progressHandle);
                C2pa.CheckError();
            }
        }
        return this;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int OnProgress(void* context, C2paProgressPhase phase, uint current, uint total)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is Func<C2paProgressPhase, uint, uint, int> callback)
        {
            try
            {
                return callback(phase, current, total);
            }
            catch
            {
                return -1;
            }
        }
        return 0;
    }

    /// <summary>
    /// Sets the HTTP resolver on the context builder. The native resolver
    /// owned by <paramref name="resolver"/> is consumed; the managed wrapper
    /// remains safe to dispose afterward.
    /// </summary>
    public ContextBuilder SetHttpResolver(HttpResolver resolver)
    {
        EnsureNotBuilt();
        unsafe
        {
            var ret = C2paBindings.context_builder_set_http_resolver(builder, resolver);
            if (ret == -1)
                C2pa.CheckError();
        }
        var resolverHandle = resolver.DetachHandle();
        if (resolverHandle.IsAllocated)
            handles.Add(resolverHandle);
        return this;
    }

    /// <summary>
    /// Consumes this builder and produces an immutable <see cref="Context"/>.
    /// </summary>
    public Context Build()
    {
        EnsureNotBuilt();
        unsafe
        {
            var ctx = C2paBindings.context_builder_build(builder);
            // The native call consumes the builder regardless of success.
            builder = null;
            if (ctx == null)
            {
                C2pa.CheckError();
            }

            return new Context(ctx, handles);
        }
    }

    public void Dispose()
    {
        unsafe
        {
            if (builder != null)
            {
                C2paBindings.free(builder);
                builder = null;
            }
        }
        handles.Dispose();
        handles = new GCHandleCollection();
    }

    private unsafe void EnsureNotBuilt()
    {
        if (builder == null)
            throw new ObjectDisposedException(nameof(ContextBuilder));
    }
}