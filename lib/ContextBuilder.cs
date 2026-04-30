// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

/// <summary>
/// Managed wrapper around the native <see cref="C2paContextBuilder"/> object.
/// Use <see cref="Build"/> to produce an immutable <see cref="Context"/>.
/// </summary>
public sealed class ContextBuilder : IDisposable
{
    private unsafe C2paContextBuilder* builder;
    private GCHandle progressHandle;

    internal unsafe ContextBuilder(C2paContextBuilder* instance)
    {
        builder = instance;
    }

    public static unsafe implicit operator C2paContextBuilder*(ContextBuilder builder)
    {
        return builder.builder;
    }

    /// <summary>
    /// Creates a new context builder with default settings.
    /// </summary>
    public static ContextBuilder Create()
    {
        unsafe
        {
            var b = C2paBindings.context_builder_new();
            if (b == null)
                C2pa.CheckError();
            return new ContextBuilder(b);
        }
    }

    /// <summary>
    /// Sets the settings on the context builder. The native settings object is
    /// consumed by this call and must not be reused by the caller.
    /// </summary>
    public unsafe ContextBuilder SetSettings(C2paSettings* settings)
    {
        EnsureNotBuilt();
        var ret = C2paBindings.context_builder_set_settings(builder, settings);
        if (ret == -1)
            C2pa.CheckError();
        return this;
    }

    public void SetSigner(ISigner signer)
    {
        EnsureNotBuilt();
        unsafe
        {
            var ret = C2paBindings.context_builder_set_signer(builder, Signer.From(signer));
            if (ret == -1)
                C2pa.CheckError();
        }
    }


    /// <summary>
    /// Sets the settings on the context builder.
    /// </summary>
    public ContextBuilder SetSettings(C2pa.Settings settings)
    {
        return SetSettings(NativeSettings.From(settings));
    }

    /// <summary>
    /// Sets the settings on the context builder. The native handle owned by
    /// <paramref name="settings"/> is consumed; the managed wrapper remains
    /// safe to dispose afterward.
    /// </summary>
    public ContextBuilder SetSettings(NativeSettings settings)
    {
        EnsureNotBuilt();
        unsafe
        {
            var ret = C2paBindings.context_builder_set_settings(builder, settings);
            if (ret == -1)
                C2pa.CheckError();
        }
        return this;
    }

    /// <summary>
    /// Sets the signer on the context builder.
    /// </summary>
    public ContextBuilder SetSigner(Signer signer)
    {
        EnsureNotBuilt();
        unsafe
        {
            var ret = C2paBindings.context_builder_set_signer(builder, signer);
            if (ret == -1)
                C2pa.CheckError();
        }
        return this;
    }

    /// <summary>
    /// Attaches a progress callback to the context builder.
    /// The callback receives the current phase along with the current and total
    /// progress values, and should return 0 to continue or non-zero to cancel.
    /// </summary>
    public ContextBuilder SetProgressCallback(Func<C2paProgressPhase, uint, uint, int> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureNotBuilt();

        if (progressHandle.IsAllocated)
            progressHandle.Free();
        progressHandle = GCHandle.Alloc(callback);

        unsafe
        {
            var ret = C2paBindings.context_builder_set_progress_callback(builder, (void*)(nint)progressHandle, &OnProgress);
            if (ret == -1)
            {
                progressHandle.Free();
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
    /// Sets the HTTP resolver on the context builder. The native resolver is
    /// consumed by this call and must not be reused by the caller.
    /// </summary>
    public unsafe ContextBuilder SetHttpResolver(C2paHttpResolver* resolver)
    {
        EnsureNotBuilt();
        var ret = C2paBindings.context_builder_set_http_resolver(builder, resolver);
        if (ret == -1)
            C2pa.CheckError();
        return this;
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
        resolver.MarkConsumed();
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
                C2pa.CheckError();
            return new Context(ctx);
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
        if (progressHandle.IsAllocated)
            progressHandle.Free();
    }

    private unsafe void EnsureNotBuilt()
    {
        if (builder == null)
            throw new ObjectDisposedException(nameof(ContextBuilder));
    }
}