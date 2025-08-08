// Copyright (c) 2025 Prakash Duggaraju. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace ContentAuthenticity.Bindings
{
    public record TrustSettings
    {

    }

    public record VerifySettings
    {

    }

    public record C2paSettings(TrustSettings Trust, VerifySettings Verify)
    {
        public string ToJson()
        {
            return Utils.Serialize(this);
        }

        public void Load()
        {
            Load(ToJson());
        }

        public static void Load(string settings, string format = "json")
        {
            var ret = c2pa.C2paLoadSettings(settings, format);
            if (ret != 0)
            {
                C2pa.CheckError();
            }
        }
    }
}
