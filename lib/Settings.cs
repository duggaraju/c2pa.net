// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

[JsonSchema("../generator/Settings.schema.json")]
public static partial class C2pa
{
    public partial class Settings
    {
        public static Settings Default => new()
        {
            Trust = new(),
            Verify = new(),
            CawgTrust = new(),
            Core = new(),
            Builder = new()
            {
                Actions = new()
                {
                    AutoCreatedAction = new(),
                    AutoOpenedAction = new(),
                    AutoPlacedAction = new(),
                    Templates = new()
                },
                Thumbnail = new()
            }
        };
    }
}