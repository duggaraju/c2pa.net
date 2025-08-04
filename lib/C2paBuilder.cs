
namespace Microsoft.ContentAuthenticity.Bindings
{

    public class C2paBuilderSettings
    {
        public string ClaimGenerator { get; set; } = string.Empty;

        public string TrustSettings { get; set; } = "{}";

    }

    public partial class C2paBuilder
    {

        private ManifestDefinition _definition;
        private readonly C2paBuilderSettings _settings;
        private readonly C2paSigner _signer;

        private ResourceStore? _resources;

        public unsafe C2paBuilder(C2paBuilderSettings settings, ISigner callback, ManifestDefinition definition) : this()
        {
            _settings = settings;
            _definition = definition;
            SignerCallback signer = (context, data, len, signature, sig_len) => Sign(callback, data, len, signature, sig_len);
            _signer = c2pa.C2paSignerCreate(nint.Zero, signer, callback.Alg, callback.Certs, callback.TimeAuthorityUrl);
        }

        public C2paBuilder(C2paBuilderSettings settings, ISigner callback, string manifestDefintionJsonString):
            this(settings, callback, ManifestDefinition.FromJson(manifestDefintionJsonString))
        {
        }

        public unsafe void Sign(Stream source, Stream dest, string format)
        {
            using var inputStream = new C2paStream(source);
            using var outputStream = new C2paStream(dest);
            _ = c2pa.C2paBuilderSign(this, format, inputStream, outputStream, _signer, null);
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

        public static C2paBuilderSettings CreateBuilderSettings(string claimGenerator, string TrustSettings = "{}")
        {
            return new C2paBuilderSettings() { ClaimGenerator = claimGenerator, TrustSettings = TrustSettings };
        }

        public ManifestDefinition GetManifestDefinition()
        {
            return _definition;
        }

        public void SetTitle(string title)
        {
            _definition.Title = title;
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