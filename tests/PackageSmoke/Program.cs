// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using ContentAuthenticity;
using ContentAuthenticity.Bindings;
using System.Runtime.InteropServices;

if (RuntimeInformation.RuntimeIdentifier != "linux-musl-x64")
{
    throw new PlatformNotSupportedException($"Expected linux-musl-x64, got {RuntimeInformation.RuntimeIdentifier}.");
}

if (args.Length != 1)
{
    throw new ArgumentException("Pass the path to the packaged musl native library.");
}

// Resolve every symbol eagerly; .NET's lazy loading can miss broken unwind dependencies.
var handle = MuslLoader.dlopen(Path.GetFullPath(args[0]), 2 /* RTLD_NOW */);
if (handle == 0)
{
    throw new DllNotFoundException(Marshal.PtrToStringUTF8(MuslLoader.dlerror()));
}
NativeLibrary.Free(handle);

Console.WriteLine($"Native SDK: {C2pa.Version}");
var fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures");
var signer = new SignerInfo(
    SigningAlg.Ps256,
    File.ReadAllText(Path.Combine(fixtures, "rs256.pub")),
    File.ReadAllText(Path.Combine(fixtures, "rs256.pem")));

using var contextBuilder = new ContextBuilder();
using var context = contextBuilder.Build();
using var builder = new Builder(context).WithDefinition("""
    {
        "title": "musl package smoke test",
        "format": "image/jpeg",
        "assertions": [
            {
                "label": "c2pa.actions",
                "data": { "actions": [{ "action": "c2pa.created" }] }
            }
        ]
    }
    """);
using var source = File.OpenRead(Path.Combine(fixtures, "no_manifest.jpg"));
using var signed = new MemoryStream();
var manifest = builder.Sign(source, signed, "image/jpeg", signer);
if (manifest.Length == 0 || signed.Length == 0)
{
    throw new InvalidOperationException("Signing produced no manifest or asset.");
}

signed.Position = 0;
using var reader = new Reader(context).WithStream(signed, "image/jpeg");
var store = reader.Store;
if (!reader.IsEmbedded ||
    store.ActiveManifest is null ||
    store.Manifests is null ||
    !store.Manifests.TryGetValue(store.ActiveManifest, out var active) ||
    active.Title != "musl package smoke test")
{
    throw new InvalidOperationException($"Signed manifest did not round-trip: {reader.Json}");
}

Console.WriteLine("Musl package native loading and signing/reading round trip passed.");

internal static class MuslLoader
{
    [DllImport("libc.musl-x86_64.so.1", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc.musl-x86_64.so.1", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint dlerror();
}