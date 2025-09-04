// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

public interface ISigner
{
    int Sign(ReadOnlySpan<byte> data, Span<byte> hash);

    public SigningAlg Alg { get; }

    public string Certs { get; }

    public string? TimeAuthorityUrl { get; }

    public bool UseOcsp => false;
}