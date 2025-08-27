
namespace Microsoft.ContentAuthenticity;

public sealed class Builder : IDisposable
{
    private readonly unsafe C2paBuilder* builder;

    private unsafe Builder(C2paBuilder* instance)
    {
        builder = instance;
    }

    public void Dispose()
    {
        unsafe
        {
            C2paBindings.builder_free(builder);
        }
    }

    public static string[] SupportedMimeTypes
    {
        get
        {
            unsafe
            {
                nuint count = 0;
                var buffer = C2paBindings.builder_supported_mime_types(&count);
                return Utils.FromCStringArray(buffer, (uint)count);
            }
        }
    }


    public static Builder Create(ManifestDefinition definition)
    {
        return FromJson(definition.ToJson());
    }

    public static Builder FromJson(string manifestDefintion)
    {
        var bytes = Encoding.UTF8.GetBytes(manifestDefintion);
        unsafe
        {
            fixed (byte* p = bytes)
            {
                var builder = C2paBindings.builder_from_json((sbyte*)p);
                if (builder == null)
                    C2pa.CheckError();
                return new Builder(builder);
            }
        }
    }

    public static Builder FromArchive(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        return FromArchive(stream);
    }


    public static Builder FromArchive(Stream stream)
    {
        using var c2paStream = new StreamAdapter(stream);
        unsafe
        {
            var builder = C2paBindings.builder_from_archive(c2paStream);
            if (builder == null)
                C2pa.CheckError();
            return new Builder(builder);
        }
    }

    public void ToArchive(string path)
    {
        using var stream = File.OpenWrite(path);
        ToArchive(stream);
    }

    public void ToArchive(Stream stream)
    {
        using var c2paStream = new StreamAdapter(stream);
        unsafe
        {
            var ret = C2paBindings.builder_to_archive(builder, c2paStream);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    public unsafe void Sign(ISigner signer, Stream source, Stream dest, string format)
    {
        var handle = GCHandle.Alloc(signer);
        try
        {
            using var inputStream = new StreamAdapter(source);
            using var outputStream = new StreamAdapter(dest);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            fixed (byte* certs = Encoding.UTF8.GetBytes(signer.Certs))
            fixed (byte* taUrl = signer.TimeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(signer.TimeAuthorityUrl))
            {
                var c2paSigner = C2paBindings.signer_create((void*)(nint)handle, &Sign, signer.Alg, (sbyte*)certs, (sbyte*)taUrl);
                if (c2paSigner == null)
                    C2pa.CheckError();
                byte* manifest = null;
                var ret = C2paBindings.builder_sign(builder, (sbyte*)formatBytes, inputStream, outputStream, c2paSigner, &manifest);
                if (ret == -1)
                    C2pa.CheckError();
                if (manifest != null)
                {
                    C2paBindings.manifest_bytes_free(manifest);
                }
            }
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Sign the input file using the provided signer and write the C2PA manifest to the output file.
    /// </summary>
    public void Sign(ISigner signer, string input, string output)
    {
        using var inputStream = new FileStream(input, FileMode.Open);
        using var outputStream = new FileStream(output, FileMode.Create);
        Sign(signer, inputStream, outputStream, Utils.GetMimeTypeFromExtension(Path.GetExtension(input)));
    }

    /// <summary>
    /// Add a resource to the C2PA builder.
    /// </summary>
    public void AddResource(string identifier, string path)
    {
        using var stream = File.OpenRead(path);
        AddResource(identifier, stream);
    }

    public void AddResource(string identifier, Stream stream)
    {
        using StreamAdapter resourceStream = new(stream);
        unsafe
        {
            fixed (byte* identifierBytes = Encoding.UTF8.GetBytes(identifier))
            {
                var ret = C2paBindings.builder_add_resource(builder, (sbyte*)identifierBytes, resourceStream);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    /// <summary>
    /// Sets the C2PA builder to not embed the manifest in the output file.
    /// </summary>
    void SetNoEmbed()
    {
        unsafe
        {
            C2paBindings.builder_set_no_embed((C2paBuilder*)builder);
        }
    }

    public void AddIngredient(Ingredient ingredient, string file)
    {
        using var stream = File.OpenRead(file);
        AddIngredient(Utils.Serialize(ingredient), Utils.GetMimeTypeFromExtension(Path.GetExtension(file)), stream);
    }

    public void AddIngredient(string ingredientJson, string ingredientFormat, Stream stream)
    {
        using var c2paStream = new StreamAdapter(stream);
        var jsonBytes = Encoding.UTF8.GetBytes(ingredientJson);
        var formatBytes = Encoding.UTF8.GetBytes(ingredientFormat);
        unsafe
        {
            fixed (byte* format = formatBytes)
            fixed (byte* json = jsonBytes)
            {
                var ret = C2paBindings.builder_add_ingredient_from_stream(builder, (sbyte*)json, (sbyte*)format, c2paStream);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    public static string GenerateInstanceID()
    {
        return "xmp:iid:" + Guid.NewGuid().ToString();
    }


    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe nint Sign(void* context, byte* data, nuint len, byte* signature, nuint sig_max_size)
    {
        GCHandle handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is ISigner signer)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            return signer.Sign(span, hash);
        }
        return -1;
    }
}