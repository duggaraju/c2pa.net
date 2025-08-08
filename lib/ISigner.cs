// Copyright (c) 2025 Prakash Duggaraju. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using ContentAuthenticity.Bindings;

namespace ContentAuthenticity.Bindings
{
    public interface ISigner
    {
        int Sign(ReadOnlySpan<byte> data, Span<byte> hash);

        public C2paSigningAlg Alg { get; }

        public string Certs { get; }

        public string? TimeAuthorityUrl { get; }

        public bool UseOcsp => false;
    }
}