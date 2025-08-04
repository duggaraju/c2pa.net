namespace Microsoft.ContentAuthenticity.Bindings
{
    public interface ISigner
    {
        int Sign(ReadOnlySpan<byte> data, Span<byte> hash);

        public C2paSigningAlg Alg { get; }

        public string Certs { get; }

        public string? TimeAuthorityUrl { get; }

        public bool UseOcsp =>false;
    }
}