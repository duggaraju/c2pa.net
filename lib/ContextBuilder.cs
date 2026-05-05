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
    public static ContextBuilder New()
    {
        unsafe
        {
            var b = C2paBindings.context_builder_new();
            if (b == null)
                C2pa.CheckError();
            return new ContextBuilder(b);
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
        using var handle = C2paSettings.Create();
        handle.UpdateFromString(settings, format);
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
            var ret = C2paBindings.context_builder_set_settings(builder, settings);
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