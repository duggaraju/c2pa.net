
namespace Microsoft.ContentAuthenticity.Bindings
{

    public partial class C2paBuilder
    {
        private C2paSigner _signer;
        private ResourceStore? _resources;

        public static C2paBuilder Create(ManifestDefinition definition, ISigner callback)
        {
            return FromJson(definition.ToJson(), callback);
        }

        public unsafe static C2paBuilder FromJson(string manifestDefintion, ISigner callback)
        {
            var builder = c2pa.C2paBuilderFromJson(manifestDefintion);
            C2pa.CheckError();
            SignerCallback signer = (context, data, len, signature, sig_len) => Sign(callback, data, len, signature, sig_len);
            builder._signer = c2pa.C2paSignerCreate(nint.Zero, signer, callback.Alg, callback.Certs, callback.TimeAuthorityUrl);
            return builder;
        }

        public unsafe void Sign(Stream source, Stream dest, string format)
        {
            using var inputStream = new C2paStream(source);
            using var outputStream = new C2paStream(dest);
            _ = c2pa.C2paBuilderSign(this, format, inputStream, outputStream, _signer, null);
            C2pa.CheckError();
        }

        public void Sign(string input, string output)
        {
            if (!File.Exists(input))
            {
                throw new ArgumentException("Invalid file path provided.", nameof(input));
            }
            using var inputStream = new FileStream(input, FileMode.Open);
            using var outputStream = new FileStream(output, FileMode.Create);
            Sign(inputStream, outputStream, Path.GetExtension(input).Substring(1));
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