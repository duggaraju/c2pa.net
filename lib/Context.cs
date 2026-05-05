// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

/// <summary>
/// Managed wrapper around the native <see cref="C2paContext"/> object.
/// </summary>
public sealed class Context : IDisposable
{
    private readonly unsafe C2paContext* context;

    internal unsafe Context(C2paContext* instance)
    {
        context = instance;
    }

    public static unsafe implicit operator C2paContext*(Context context)
    {
        return context.context;
    }

    /// <summary>
    /// Creates a new context with default settings.
    /// </summary>
    public static Context New()
    {
        unsafe
        {
            var ctx = C2paBindings.context_new();
            if (ctx == null)
                C2pa.CheckError();
            return new Context(ctx);
        }
    }

    /// <summary>
    /// Requests cancellation of any in-progress operation on this context.
    /// </summary>
    public void Cancel()
    {
        unsafe
        {
            var ret = C2paBindings.context_cancel(context);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    public void Dispose()
    {
        unsafe
        {
            C2paBindings.free(context);
        }
    }
}