// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;

namespace ContentAuthenticity.Bindings;

internal static class NativeLibraryResolver
{
    private const string ImportName = "c2pa_c";

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute is only intended to be used in application code
    [ModuleInitializer]
    internal static void Init()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
    }
#pragma warning restore CA2255

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, ImportName, StringComparison.Ordinal))
        {
            return nint.Zero;
        }

        string? fileName = GetNativeFileName();
        if (fileName is null)
        {
            return nint.Zero;
        }

        string baseDir = AppContext.BaseDirectory;

        var platform = OperatingSystem.IsWindows() ? OSPlatform.Windows
            : OperatingSystem.IsLinux() ? OSPlatform.Linux
            : OSPlatform.OSX;
        foreach (string rid in GetCandidateRids(
            RuntimeInformation.RuntimeIdentifier,
            platform,
            RuntimeInformation.ProcessArchitecture,
            OperatingSystem.IsLinux() && File.Exists("/etc/alpine-release")))
        {
            string candidate = Path.Combine(baseDir, "runtimes", rid, "native", fileName);
            if (NativeLibrary.TryLoad(candidate, out nint handle))
            {
                return handle;
            }
        }

        string candidateInRoot = Path.Combine(baseDir, fileName);
        if (NativeLibrary.TryLoad(candidateInRoot, out nint rootHandle))
        {
            return rootHandle;
        }

        return nint.Zero;
    }

    internal static IEnumerable<string> GetCandidateRids(
        string runtimeRid, OSPlatform platform, Architecture architecture, bool isAlpine)
    {
        yield return runtimeRid;

        string? fallbackRid = GetFallbackRid(runtimeRid, platform, architecture, isAlpine);
        if (fallbackRid is not null &&
            !string.Equals(fallbackRid, runtimeRid, StringComparison.Ordinal))
        {
            yield return fallbackRid;
        }
    }

    private static string? GetFallbackRid(
        string runtimeRid, OSPlatform platform, Architecture architecture, bool isAlpine)
    {
        string? arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
        if (arch is null)
        {
            return null;
        }

        if (platform == OSPlatform.Windows) return $"win-{arch}";
        if (platform == OSPlatform.OSX) return $"osx-{arch}";

        if (platform == OSPlatform.Linux)
        {
            // RuntimeIdentifier identifies musl for portable .NET runtimes. Alpine's
            // distro-built runtime can expose an Alpine-specific RID instead.
            bool isMusl = runtimeRid.StartsWith("linux-musl-", StringComparison.Ordinal) || isAlpine;
            return isMusl
                ? $"linux-musl-{arch}"
                : $"linux-{arch}";
        }

        return null;
    }

    private static string? GetNativeFileName()
    {
        if (OperatingSystem.IsWindows()) return "c2pa_c.dll";
        if (OperatingSystem.IsLinux()) return "libc2pa_c.so";
        if (OperatingSystem.IsMacOS()) return "libc2pa_c.dylib";
        return null;
    }
}