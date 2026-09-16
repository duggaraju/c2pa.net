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

        foreach (string rid in GetCandidateRids())
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

    private static IEnumerable<string> GetCandidateRids()
    {
        string runtimeRid = RuntimeInformation.RuntimeIdentifier;
        yield return runtimeRid;

        string? fallbackRid = GetFallbackRid();
        if (fallbackRid is not null &&
            !string.Equals(fallbackRid, runtimeRid, StringComparison.Ordinal))
        {
            yield return fallbackRid;
        }
    }

    private static string? GetFallbackRid()
    {
        string? arch = GetArchPart();
        if (arch is null)
        {
            return null;
        }

        if (OperatingSystem.IsWindows()) return $"win-{arch}";
        if (OperatingSystem.IsMacOS()) return $"osx-{arch}";

        if (OperatingSystem.IsLinux())
        {
            // RuntimeIdentifier identifies musl for portable .NET runtimes. Alpine's
            // distro-built runtime can expose an Alpine-specific RID instead.
            return File.Exists("/etc/alpine-release")
                ? $"linux-musl-{arch}"
                : $"linux-{arch}";
        }

        return null;
    }

    private static string? GetArchPart() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

    private static string? GetNativeFileName()
    {
        if (OperatingSystem.IsWindows()) return "c2pa_c.dll";
        if (OperatingSystem.IsLinux()) return "libc2pa_c.so";
        if (OperatingSystem.IsMacOS()) return "libc2pa_c.dylib";
        return null;
    }
}