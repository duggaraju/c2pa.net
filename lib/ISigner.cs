using SigningAlg = Microsoft.ContentAuthenticity.Bindings.C2paSigningAlg;
namespace Microsoft.ContentAuthenticity
{
    public interface ISigner
    {
        int Sign(ReadOnlySpan<byte> data, Span<byte> hash);

        public SigningAlg Alg { get; }

        public string Certs { get; }

        public string? TimeAuthorityUrl { get; }

        public bool UseOcsp => false;
    }
}