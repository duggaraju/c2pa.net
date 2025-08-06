
namespace Microsoft.ContentAuthenticity.Bindings
{

    public partial class C2paBuilder
    {
        private ResourceStore? _resources;

        public static C2paBuilder Create(ManifestDefinition definition)
        {
            return FromJson(definition.ToJson());
        }

        public unsafe static C2paBuilder FromJson(string manifestDefintion)
        {
            var builder = c2pa.C2paBuilderFromJson(manifestDefintion);
            C2pa.CheckError();
            return builder;
        }

        partial void DisposePartial(bool disposing)
        {
            if (disposing)
            {
                c2pa.C2paBuilderFree(this);
                C2pa.CheckError();
            }
        }


        public unsafe void Sign(ISigner signer, Stream source, Stream dest, string format)
        {
            SignerCallback callback = (context, data, len, signature, sig_len) => Sign(signer, data, len, signature, sig_len);
            var c2paSigner = c2pa.C2paSignerCreate(nint.Zero, callback, signer.Alg, signer.Certs, signer.TimeAuthorityUrl);
            using var inputStream = new C2paStream(source);
            using var outputStream = new C2paStream(dest);
            byte* manifest = null;
            var ret = c2pa.C2paBuilderSign(this, format, inputStream, outputStream, c2paSigner, &manifest);
            if (ret == -1)
                C2pa.CheckError();
            if (manifest != null)
            {
                c2pa.C2paManifestBytesFree(manifest);
                C2pa.CheckError();
            }
        }

        public void Sign(ISigner signer, string input, string output)
        {
            if (!File.Exists(input))
            {
                throw new ArgumentException("Invalid file path provided.", nameof(input));
            }
            using var inputStream = new FileStream(input, FileMode.Open);
            using var outputStream = new FileStream(output, FileMode.Create);
            Sign(signer, inputStream, outputStream, Utils.GetMimeTypeFromExtension(Path.GetExtension(input)));
        }

        public void AddResource(string identifier, string path)
        {
            _resources ??= new ResourceStore();
            _resources.Resources.Add(identifier, path);
            using C2paStream resourceStream = new(new FileStream(path, FileMode.Open));
            c2pa.C2paBuilderAddResource(this, identifier, resourceStream);
            C2pa.CheckError();
        }

        public static string GenerateInstanceID()
        {
            return "xmp:iid:" + Guid.NewGuid().ToString();
        }

        private static unsafe long Sign(ISigner callback, byte* data, ulong len, byte* signature, ulong sig_max_size)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            return callback.Sign(span, hash);
        }
    }

}