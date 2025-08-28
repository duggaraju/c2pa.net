using SigningAlg = ContentAuthenticity.Bindings.C2paSigningAlg;

namespace ContentAuthenticity;

public interface ISigner
{
    int Sign(ReadOnlySpan<byte> data, Span<byte> hash);

    public SigningAlg Alg { get; }

    public string Certs { get; }

    public string? TimeAuthorityUrl { get; }

    public bool UseOcsp => false;

    public string? EKUs {  get; }

    public string? TrustAnchors { get; }
}