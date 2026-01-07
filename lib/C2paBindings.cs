using System;
using System.Runtime.InteropServices;

namespace ContentAuthenticity.Bindings;

public static unsafe partial class C2paBindings
{
    private enum BindingImpl
    {
        WindowsX64,
        WindowsArm64,
        LinuxX64,
        LinuxArm64,
        OsxX64,
        OsxArm64,
    }

    private static readonly BindingImpl _impl = DetermineImpl();

    private static BindingImpl DetermineImpl()
    {
        var arch = RuntimeInformation.ProcessArchitecture;

        if (OperatingSystem.IsWindows())
        {
            return arch switch
            {
                Architecture.X64 => BindingImpl.WindowsX64,
                Architecture.Arm64 => BindingImpl.WindowsArm64,
                _ => throw new PlatformNotSupportedException($"Unsupported architecture '{arch}' on Windows."),
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return arch switch
            {
                Architecture.X64 => BindingImpl.LinuxX64,
                Architecture.Arm64 => BindingImpl.LinuxArm64,
                _ => throw new PlatformNotSupportedException($"Unsupported architecture '{arch}' on Linux."),
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return arch switch
            {
                Architecture.X64 => BindingImpl.OsxX64,
                Architecture.Arm64 => BindingImpl.OsxArm64,
                _ => throw new PlatformNotSupportedException($"Unsupported architecture '{arch}' on macOS."),
            };
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    public static C2paStream* create_stream(StreamContext* context, delegate* unmanaged[Cdecl]<StreamContext*, byte*, nint, nint> reader, delegate* unmanaged[Cdecl]<StreamContext*, nint, C2paSeekMode, nint> seeker, delegate* unmanaged[Cdecl]<StreamContext*, byte*, nint, nint> writer, delegate* unmanaged[Cdecl]<StreamContext*, nint> flusher)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.create_stream(context, reader, seeker, writer, flusher),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.create_stream(context, reader, seeker, writer, flusher),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.create_stream(context, reader, seeker, writer, flusher),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.create_stream(context, reader, seeker, writer, flusher),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.create_stream(context, reader, seeker, writer, flusher),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.create_stream(context, reader, seeker, writer, flusher),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void release_stream(C2paStream* stream)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.release_stream(stream); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.release_stream(stream); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.release_stream(stream); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.release_stream(stream); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.release_stream(stream); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.release_stream(stream); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static sbyte* version()
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.version(),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.version(),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.version(),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.version(),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.version(),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.version(),
            _ => throw new PlatformNotSupportedException(),
        };

    public static sbyte* error()
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.error(),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.error(),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.error(),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.error(),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.error(),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.error(),
            _ => throw new PlatformNotSupportedException(),
        };

    public static int load_settings(sbyte* settings, sbyte* format)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.load_settings(settings, format),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.load_settings(settings, format),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.load_settings(settings, format),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.load_settings(settings, format),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.load_settings(settings, format),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.load_settings(settings, format),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void string_free(sbyte* s)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.string_free(s); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.string_free(s); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.string_free(s); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.string_free(s); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.string_free(s); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.string_free(s); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static void free_string_array(sbyte** ptr, nuint count)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.free_string_array(ptr, count); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.free_string_array(ptr, count); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.free_string_array(ptr, count); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.free_string_array(ptr, count); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.free_string_array(ptr, count); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.free_string_array(ptr, count); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static C2paReader* reader_from_stream(sbyte* format, C2paStream* stream)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.reader_from_stream(format, stream),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.reader_from_stream(format, stream),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.reader_from_stream(format, stream),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.reader_from_stream(format, stream),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.reader_from_stream(format, stream),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.reader_from_stream(format, stream),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void reader_free(C2paReader* reader_ptr)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.reader_free(reader_ptr); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.reader_free(reader_ptr); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.reader_free(reader_ptr); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.reader_free(reader_ptr); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.reader_free(reader_ptr); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.reader_free(reader_ptr); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static sbyte* reader_json(C2paReader* reader_ptr)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.reader_json(reader_ptr),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.reader_json(reader_ptr),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.reader_json(reader_ptr),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.reader_json(reader_ptr),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.reader_json(reader_ptr),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.reader_json(reader_ptr),
            _ => throw new PlatformNotSupportedException(),
        };

    public static byte reader_is_embedded(C2paReader* reader_ptr)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.reader_is_embedded(reader_ptr),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.reader_is_embedded(reader_ptr),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.reader_is_embedded(reader_ptr),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.reader_is_embedded(reader_ptr),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.reader_is_embedded(reader_ptr),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.reader_is_embedded(reader_ptr),
            _ => throw new PlatformNotSupportedException(),
        };

    public static sbyte** reader_supported_mime_types(nuint* count)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.reader_supported_mime_types(count),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.reader_supported_mime_types(count),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.reader_supported_mime_types(count),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.reader_supported_mime_types(count),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.reader_supported_mime_types(count),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.reader_supported_mime_types(count),
            _ => throw new PlatformNotSupportedException(),
        };

    public static C2paBuilder* builder_from_json(sbyte* manifest_json)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_from_json(manifest_json),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_from_json(manifest_json),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_from_json(manifest_json),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_from_json(manifest_json),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_from_json(manifest_json),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_from_json(manifest_json),
            _ => throw new PlatformNotSupportedException(),
        };

    public static C2paBuilder* builder_from_archive(C2paStream* stream)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_from_archive(stream),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_from_archive(stream),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_from_archive(stream),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_from_archive(stream),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_from_archive(stream),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_from_archive(stream),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void builder_free(C2paBuilder* builder_ptr)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.builder_free(builder_ptr); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.builder_free(builder_ptr); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.builder_free(builder_ptr); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.builder_free(builder_ptr); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.builder_free(builder_ptr); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.builder_free(builder_ptr); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static sbyte** builder_supported_mime_types(nuint* count)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_supported_mime_types(count),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_supported_mime_types(count),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_supported_mime_types(count),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_supported_mime_types(count),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_supported_mime_types(count),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_supported_mime_types(count),
            _ => throw new PlatformNotSupportedException(),
        };

    public static int builder_to_archive(C2paBuilder* builder_ptr, C2paStream* stream)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_to_archive(builder_ptr, stream),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_to_archive(builder_ptr, stream),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_to_archive(builder_ptr, stream),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_to_archive(builder_ptr, stream),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_to_archive(builder_ptr, stream),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_to_archive(builder_ptr, stream),
            _ => throw new PlatformNotSupportedException(),
        };

    public static int builder_add_resource(C2paBuilder* builder_ptr, sbyte* uri, C2paStream* stream)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_add_resource(builder_ptr, uri, stream),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_add_resource(builder_ptr, uri, stream),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_add_resource(builder_ptr, uri, stream),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_add_resource(builder_ptr, uri, stream),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_add_resource(builder_ptr, uri, stream),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_add_resource(builder_ptr, uri, stream),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void builder_set_no_embed(C2paBuilder* builder_ptr)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.builder_set_no_embed(builder_ptr); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.builder_set_no_embed(builder_ptr); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.builder_set_no_embed(builder_ptr); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.builder_set_no_embed(builder_ptr); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.builder_set_no_embed(builder_ptr); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.builder_set_no_embed(builder_ptr); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static int builder_set_remote_url(C2paBuilder* builder_ptr, sbyte* remote_url)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_set_remote_url(builder_ptr, remote_url),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_set_remote_url(builder_ptr, remote_url),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_set_remote_url(builder_ptr, remote_url),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_set_remote_url(builder_ptr, remote_url),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_set_remote_url(builder_ptr, remote_url),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_set_remote_url(builder_ptr, remote_url),
            _ => throw new PlatformNotSupportedException(),
        };

    public static int builder_set_base_path(C2paBuilder* builder_ptr, sbyte* base_path)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_set_base_path(builder_ptr, base_path),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_set_base_path(builder_ptr, base_path),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_set_base_path(builder_ptr, base_path),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_set_base_path(builder_ptr, base_path),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_set_base_path(builder_ptr, base_path),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_set_base_path(builder_ptr, base_path),
            _ => throw new PlatformNotSupportedException(),
        };

    public static int builder_add_ingredient_from_stream(C2paBuilder* builder_ptr, sbyte* ingredient_json, sbyte* format, C2paStream* source)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_add_ingredient_from_stream(builder_ptr, ingredient_json, format, source),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_add_ingredient_from_stream(builder_ptr, ingredient_json, format, source),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_add_ingredient_from_stream(builder_ptr, ingredient_json, format, source),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_add_ingredient_from_stream(builder_ptr, ingredient_json, format, source),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_add_ingredient_from_stream(builder_ptr, ingredient_json, format, source),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_add_ingredient_from_stream(builder_ptr, ingredient_json, format, source),
            _ => throw new PlatformNotSupportedException(),
        };

    public static C2paSigner* signer_create(void* context, delegate* unmanaged[Cdecl]<void*, byte*, nuint, byte*, nuint, nint> callback, SigningAlg alg, sbyte* certs, sbyte* tsa_url)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.signer_create(context, callback, alg, certs, tsa_url),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.signer_create(context, callback, alg, certs, tsa_url),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.signer_create(context, callback, alg, certs, tsa_url),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.signer_create(context, callback, alg, certs, tsa_url),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.signer_create(context, callback, alg, certs, tsa_url),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.signer_create(context, callback, alg, certs, tsa_url),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void signer_free(C2paSigner* signer_ptr)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.signer_free(signer_ptr); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.signer_free(signer_ptr); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.signer_free(signer_ptr); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.signer_free(signer_ptr); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.signer_free(signer_ptr); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.signer_free(signer_ptr); break;
            default: throw new PlatformNotSupportedException();
        }
    }

    public static nint builder_sign(C2paBuilder* builder_ptr, sbyte* format, C2paStream* source, C2paStream* dest, C2paSigner* signer_ptr, byte** manifest_bytes_ptr)
        => _impl switch
        {
            BindingImpl.WindowsX64 => C2paBindings_Windows_X64.builder_sign(builder_ptr, format, source, dest, signer_ptr, manifest_bytes_ptr),
            BindingImpl.WindowsArm64 => C2paBindings_Windows_Arm64.builder_sign(builder_ptr, format, source, dest, signer_ptr, manifest_bytes_ptr),
            BindingImpl.LinuxX64 => C2paBindings_Linux_X64.builder_sign(builder_ptr, format, source, dest, signer_ptr, manifest_bytes_ptr),
            BindingImpl.LinuxArm64 => C2paBindings_Linux_Arm64.builder_sign(builder_ptr, format, source, dest, signer_ptr, manifest_bytes_ptr),
            BindingImpl.OsxX64 => C2paBindings_OSX_X64.builder_sign(builder_ptr, format, source, dest, signer_ptr, manifest_bytes_ptr),
            BindingImpl.OsxArm64 => C2paBindings_OSX_Arm64.builder_sign(builder_ptr, format, source, dest, signer_ptr, manifest_bytes_ptr),
            _ => throw new PlatformNotSupportedException(),
        };

    public static void manifest_bytes_free(byte* manifest_bytes_ptr)
    {
        switch (_impl)
        {
            case BindingImpl.WindowsX64: C2paBindings_Windows_X64.manifest_bytes_free(manifest_bytes_ptr); break;
            case BindingImpl.WindowsArm64: C2paBindings_Windows_Arm64.manifest_bytes_free(manifest_bytes_ptr); break;
            case BindingImpl.LinuxX64: C2paBindings_Linux_X64.manifest_bytes_free(manifest_bytes_ptr); break;
            case BindingImpl.LinuxArm64: C2paBindings_Linux_Arm64.manifest_bytes_free(manifest_bytes_ptr); break;
            case BindingImpl.OsxX64: C2paBindings_OSX_X64.manifest_bytes_free(manifest_bytes_ptr); break;
            case BindingImpl.OsxArm64: C2paBindings_OSX_Arm64.manifest_bytes_free(manifest_bytes_ptr); break;
            default: throw new PlatformNotSupportedException();
        }
    }
}