using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

        string? osPart = GetOsPart();
        string? archPart = GetArchPart();
        string? fileName = GetNativeFileName();

        if (osPart is null || archPart is null || fileName is null)
        {
            return nint.Zero;
        }

        string baseDir = AppContext.BaseDirectory;
        string rid = $"{osPart}-{archPart}";

        string candidate = Path.Combine(baseDir, "runtimes", rid, "native", fileName);
        if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle))
        {
            return handle;
        }

        // Fallback: some runners copy native binaries to the output root.
        candidate = Path.Combine(baseDir, fileName);
        if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out handle))
        {
            return handle;
        }

        return nint.Zero;
    }

    private static string? GetOsPart()
    {
        if (OperatingSystem.IsWindows()) return "win";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS()) return "osx";
        return null;
    }

    private static string? GetArchPart()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
    }

    private static string? GetNativeFileName()
    {
        if (OperatingSystem.IsWindows()) return "c2pa_c.dll";
        if (OperatingSystem.IsLinux()) return "libc2pa_c.so";
        if (OperatingSystem.IsMacOS()) return "libc2pa_c.dylib";
        return null;
    }
}