// Copyright (c) 2025 Prakash Duggaraju. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace ContentAuthenticity.Bindings
{
    [Serializable]
    public class C2paException(string type, string message) : Exception(message)
    {
        public readonly string Type = type;
    }
}