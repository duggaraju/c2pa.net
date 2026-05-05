// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

[JsonSchema("../c2pa-rs/target/schema/Builder.schema.json", "ManifestDefinition")]
public partial class Builder : IDisposable
{
    private unsafe C2paBuilder* handle;

    internal unsafe Builder(C2paBuilder* instance)
    {
        handle = instance;
    }

    public static unsafe implicit operator C2paBuilder*(Builder builder)
    {
        return builder.handle;
    }

    public void Dispose()
    {
        unsafe
        {
            C2paBindings.free(handle);
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


    public static Builder FromContext(Context context)
    {
        unsafe
        {
            var builder = C2paBindings.builder_from_context(context);
            if (builder == null)
                C2pa.CheckError();
            return new Builder(builder);
        }
    }

    public Builder WithDefinition(ManifestDefinition manifest)
    {
        return WithDefinition(manifest.ToJson());
    }

    public Builder WithDefinition(string manifest)
    {
        var bytes = Encoding.UTF8.GetBytes(manifest);
        unsafe
        {
            fixed (byte* p = bytes)
            {
                var builder = C2paBindings.builder_with_definition(this, (sbyte*)p);
                if (builder == null)
                    C2pa.CheckError();
                this.handle = builder;
                return this;
            }
        }
    }

    public Builder WithArchive(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        return WithArchive(stream);
    }

    public Builder WithArchive(Stream stream)
    {
        using var c2paStream = new StreamAdapter(stream);
        unsafe
        {
            var builder = C2paBindings.builder_with_archive(handle, c2paStream);
            if (builder == null)
                C2pa.CheckError();
            handle = builder;
            return this;
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
            var ret = C2paBindings.builder_to_archive(handle, c2paStream);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    public byte[] Sign(ISigner signer, Stream source, Stream dest, string format)
    {
        using var s = Signer.From(signer);
        return Sign(s, source, dest, format);
    }

    public byte[] Sign(Signer signer, Stream source, Stream dest, string format)
    {
        using var inputStream = new StreamAdapter(source);
        using var outputStream = new StreamAdapter(dest);
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                byte* manifest = null;
                var ret = C2paBindings.builder_sign(handle, (sbyte*)formatBytes, inputStream, outputStream, signer, &manifest);
                if (ret == -1)
                    C2pa.CheckError();
                var bytes = new byte[ret];
                Marshal.Copy((nint)manifest, bytes, 0, bytes.Length);
                C2paBindings.free(manifest);
                return bytes;
            }
        }
    }

    public byte[] Sign(Stream source, Stream dest, string format)
    {
        using var inputStream = new StreamAdapter(source);
        using var outputStream = new StreamAdapter(dest);
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                byte* manifest = null;
                var ret = C2paBindings.builder_sign_context(handle, (sbyte*)formatBytes, inputStream, outputStream, &manifest);
                if (ret == -1)
                    C2pa.CheckError();
                var bytes = new byte[ret];
                Marshal.Copy((nint)manifest, bytes, 0, bytes.Length);
                C2paBindings.free(manifest);
                return bytes;
            }
        }
    }

    /// <summary>
    /// Sign the input file using the provided signer and write the C2PA manifest to the output file.
    /// </summary>
    public byte[] Sign(ISigner signer, string input, string output)
    {
        using var inputStream = new FileStream(input, FileMode.Open);
        using var outputStream = new FileStream(output, FileMode.Create);
        return Sign(signer, inputStream, outputStream, input.GetMimeType());
    }

    public byte[] Sign(string input, string output)
    {
        using var inputStream = new FileStream(input, FileMode.Open);
        using var outputStream = new FileStream(output, FileMode.Create);
        return Sign(inputStream, outputStream, input.GetMimeType());
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
                var ret = C2paBindings.builder_add_resource(handle, (sbyte*)identifierBytes, resourceStream);
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
            C2paBindings.builder_set_no_embed(handle);
        }
    }

    void SetIntent(C2paBuilderIntent intent, C2paDigitalSourceType sourceType)
    {
        unsafe
        {
            var ret = C2paBindings.builder_set_intent(handle, intent, sourceType);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    void SetBasePath(string path)
    {
        unsafe
        {
            fixed (byte* pathBytes = Encoding.UTF8.GetBytes(path))
            {
                var ret = C2paBindings.builder_set_base_path(handle, (sbyte*)pathBytes);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    void SetRemoteUrl(Uri uri)
    {
        unsafe
        {
            fixed (byte* urlBytes = Encoding.UTF8.GetBytes(uri.ToString()))
            {
                var ret = C2paBindings.builder_set_remote_url(handle, (sbyte*)urlBytes);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    /**
    * Adds an action assertion to the manifest.
    */
    void AddAction(ActionV2 action)
    {
        var actionJson = action.ToJson();
        var actionBytes = Encoding.UTF8.GetBytes(actionJson);
        unsafe
        {
            fixed (byte* json = actionBytes)
            {
                var ret = C2paBindings.builder_add_action(handle, (sbyte*)json);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    public void AddIngredient(Ingredient ingredient, string file)
    {
        using var stream = File.OpenRead(file);
        AddIngredient(ingredient.ToJson(), Utils.GetMimeTypeFromExtension(Path.GetExtension(file)), stream);
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
                var ret = C2paBindings.builder_add_ingredient_from_stream(handle, (sbyte*)json, (sbyte*)format, c2paStream);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    /// <summary>
    /// Adds an ingredient to this builder from a C2PA ingredient archive
    /// stream previously written by <see cref="WriteIngredientArchive"/>.
    /// </summary>
    public void AddIngredientFromArchive(Stream stream)
    {
        using var c2paStream = new StreamAdapter(stream);
        unsafe
        {
            var ret = C2paBindings.builder_add_ingredient_from_archive(handle, c2paStream);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    /// <summary>
    /// Writes a single-ingredient C2PA archive to the destination stream.
    /// The archive can later be loaded with <see cref="AddIngredientFromArchive"/>.
    /// Requires the <c>generate_c2pa_archive</c> builder setting to be enabled.
    /// </summary>
    public void WriteIngredientToArchive(string ingredientId, Stream stream)
    {
        using var c2paStream = new StreamAdapter(stream);
        unsafe
        {
            fixed (byte* idBytes = Encoding.UTF8.GetBytes(ingredientId))
            {
                var ret = C2paBindings.builder_write_ingredient_archive(handle, (sbyte*)idBytes, c2paStream);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    /// <summary>
    /// Returns whether a placeholder manifest is required for the given format.
    /// </summary>
    public bool NeedsPlaceholder(string format)
    {
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                var ret = C2paBindings.builder_needs_placeholder(handle, (sbyte*)formatBytes);
                if (ret == -1)
                    C2pa.CheckError();
                return ret == 1;
            }
        }
    }

    /// <summary>
    /// Returns the hash binding type that this builder will use for the given format.
    /// </summary>
    public C2paHashType GetHashType(string format)
    {
        unsafe
        {
            C2paHashType hashType;
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                var ret = C2paBindings.builder_hash_type(handle, (sbyte*)formatBytes, &hashType);
                if (ret == -1)
                    C2pa.CheckError();
            }
            return hashType;
        }
    }

    /// <summary>
    /// Creates a composed placeholder manifest for the given format. The
    /// placeholder bytes can be embedded directly into an asset to reserve
    /// space for the final signed manifest.
    /// </summary>
    public byte[] Placeholder(string format)
    {
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                byte* manifest = null;
                var ret = C2paBindings.builder_placeholder(handle, (sbyte*)formatBytes, &manifest);
                if (ret == -1)
                    C2pa.CheckError();
                var bytes = new byte[ret];
                if (ret > 0 && manifest != null)
                {
                    Marshal.Copy((nint)manifest, bytes, 0, bytes.Length);
                    C2paBindings.free(manifest);
                }
                return bytes;
            }
        }
    }

    /// <summary>
    /// Reserves a placeholder manifest of the given size for a DataHash
    /// signing workflow.
    /// </summary>
    public byte[] DataHashedPlaceholder(nuint reservedSize, string format)
    {
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                byte* manifest = null;
                var ret = C2paBindings.builder_data_hashed_placeholder(handle, reservedSize, (sbyte*)formatBytes, &manifest);
                if (ret == -1)
                    C2pa.CheckError();
                var bytes = new byte[ret];
                if (ret > 0 && manifest != null)
                {
                    Marshal.Copy((nint)manifest, bytes, 0, bytes.Length);
                    C2paBindings.free(manifest);
                }
                return bytes;
            }
        }
    }

    /// <summary>
    /// Signs the manifest and returns composed bytes ready for embedding.
    /// Operates in placeholder mode (after <see cref="Placeholder"/>) or
    /// direct mode (when a hard binding assertion already exists).
    /// </summary>
    public byte[] SignEmbeddable(string format)
    {
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                byte* manifest = null;
                var ret = C2paBindings.builder_sign_embeddable(handle, (sbyte*)formatBytes, &manifest);
                if (ret == -1)
                    C2pa.CheckError();
                var bytes = new byte[ret];
                if (ret > 0 && manifest != null)
                {
                    Marshal.Copy((nint)manifest, bytes, 0, bytes.Length);
                    C2paBindings.free(manifest);
                }
                return bytes;
            }
        }
    }

    /// <summary>
    /// Signs this builder using the specified signer and a JSON DataHash
    /// description. This is a low-level method for advanced use cases where
    /// the caller handles embedding the manifest.
    /// </summary>
    /// <param name="signer">The signer to use.</param>
    /// <param name="dataHashJson">JSON string containing DataHash information.</param>
    /// <param name="format">MIME type or extension of the asset.</param>
    /// <param name="asset">Optional asset stream. If <c>null</c> pre-calculated hashes are used.</param>
    public byte[] SignDataHashedEmbeddable(ISigner signer, string dataHashJson, string format, Stream? asset = null)
    {
        using var s = Signer.From(signer);
        return SignDataHashedEmbeddable(s, dataHashJson, format, asset);
    }

    public byte[] SignDataHashedEmbeddable(Signer signer, string dataHashJson, string format, Stream? asset = null)
    {
        StreamAdapter? assetStream = asset == null ? null : new StreamAdapter(asset);
        try
        {
            unsafe
            {
                fixed (byte* dataHashBytes = Encoding.UTF8.GetBytes(dataHashJson))
                fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
                {
                    byte* manifest = null;
                    C2paStream* nativeAsset = assetStream == null ? null : (C2paStream*)assetStream;
                    var ret = C2paBindings.builder_sign_data_hashed_embeddable(handle, signer, (sbyte*)dataHashBytes, (sbyte*)formatBytes, nativeAsset, &manifest);
                    if (ret == -1)
                        C2pa.CheckError();
                    var bytes = new byte[ret];
                    if (ret > 0 && manifest != null)
                    {
                        Marshal.Copy((nint)manifest, bytes, 0, bytes.Length);
                        C2paBindings.free(manifest);
                    }
                    return bytes;
                }
            }
        }
        finally
        {
            assetStream?.Dispose();
        }
    }

    /// <summary>
    /// Sets the byte exclusion ranges on the DataHash assertion. Each pair is
    /// <c>(start, length)</c> in bytes.
    /// </summary>
    public void SetDataHashExclusions(IReadOnlyList<(ulong Start, ulong Length)> exclusions)
    {
        var flat = new ulong[exclusions.Count * 2];
        for (int i = 0; i < exclusions.Count; i++)
        {
            flat[i * 2] = exclusions[i].Start;
            flat[i * 2 + 1] = exclusions[i].Length;
        }
        unsafe
        {
            fixed (ulong* p = flat)
            {
                var ret = C2paBindings.builder_set_data_hash_exclusions(handle, p, (nuint)exclusions.Count);
                if (ret != 0)
                    C2pa.CheckError();
            }
        }
    }

    /// <summary>
    /// Configures the hasher to produce a Merkle tree per mdat using fixed
    /// size leaf chunks (in KB).
    /// </summary>
    public void SetFixedSizeMerkle(nuint fixedSizeKb)
    {
        unsafe
        {
            var ret = C2paBindings.builder_set_fixed_size_merkle(handle, fixedSizeKb);
            if (ret == -1)
                C2pa.CheckError();
        }
    }

    /// <summary>
    /// Generates mdat leaf hashes for the asset. Data should be supplied in
    /// the order it is written to the mdat.
    /// </summary>
    public void HashMdatBytes(nuint mdatId, ReadOnlySpan<byte> data, bool largeSize)
    {
        unsafe
        {
            fixed (byte* dataPtr = data)
            {
                var ret = C2paBindings.builder_hash_mdat_bytes(handle, mdatId, dataPtr, (nuint)data.Length, largeSize ? (byte)1 : (byte)0);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    /// <summary>
    /// Updates the hard binding assertion (DataHash, BmffHash or BoxHash) by
    /// hashing the supplied asset stream.
    /// </summary>
    public void UpdateHashFromStream(Stream stream, string format)
    {
        using var c2paStream = new StreamAdapter(stream);
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                var ret = C2paBindings.builder_update_hash_from_stream(handle, (sbyte*)formatBytes, c2paStream);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }
    }

    public static string GenerateInstanceID() => $"xmp:iid:{Guid.NewGuid()}";
}