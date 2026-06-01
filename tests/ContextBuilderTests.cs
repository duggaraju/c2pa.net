// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace ContentAuthenticity.Tests;

public class ContextBuilderTests
{
    [Fact]
    public void SetProgressCallback_BuildTransfersHandleOwnershipToContext()
    {
        using var builder = new ContextBuilder();
        Func<C2paProgressPhase, uint, uint, int> callback = (_, _, _) => 1;

        builder.SetProgressCallback(callback);

        var builderHandleBeforeBuild = GetProgressHandle(builder);
        Assert.True(builderHandleBeforeBuild.IsAllocated);
        Assert.Same(callback, builderHandleBeforeBuild.Target);

        using var context = builder.Build();

        var builderHandleAfterBuild = GetProgressHandle(builder);
        Assert.False(builderHandleAfterBuild.IsAllocated);

        var contextHandle = GetProgressHandle(context);
        Assert.True(contextHandle.IsAllocated);
        Assert.Same(callback, contextHandle.Target);
    }

    [Fact]
    public void SetProgressCallback_CallbackRemainsAliveAfterBuilderIsDisposed()
    {
        var callbackCount = 0;
        var inputPath = Path.Combine(AppContext.BaseDirectory, "basic-signed.pdf");
        Assert.True(File.Exists(inputPath), $"Missing test fixture: {inputPath}");

        using var builder = new ContextBuilder();
        builder.SetProgressCallback((_, _, _) =>
        {
            Interlocked.Increment(ref callbackCount);
            return 1;
        });

        using var context = builder.Build();
        builder.Dispose();

        var builderHandleAfterDispose = GetProgressHandle(builder);
        Assert.False(builderHandleAfterDispose.IsAllocated);

        var contextHandleBeforeRead = GetProgressHandle(context);
        Assert.True(contextHandleBeforeRead.IsAllocated);

        var exception = Record.Exception(() =>
        {
            using var reader = new Reader(context).WithFile(inputPath);
            _ = reader.Json;
        });

        Assert.True(exception == null || exception is C2paException);
        Assert.True(Volatile.Read(ref callbackCount) > 0);
    }

    private static GCHandle GetProgressHandle(object instance)
    {
        var field = instance.GetType().GetField("progressHandle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (GCHandle)field!.GetValue(instance)!;
    }
}