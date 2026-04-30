// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;


/// <summary>
/// Schema-generated root DTO for c2pa settings. Renamed from "Settings" to
/// avoid collision with the enclosing wrapper class.
/// </summary>
[JsonSchema("../c2pa-rs/target/schema/Settings.schema.json")]
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
                    AutoOpenedAction = new()
                    {
                        Enabled = false,
                    },
                    AutoPlacedAction = new()
                    {
                        Enabled = false,
                    },
                    Templates = []
                },
                Thumbnail = new(),
                AutoTimestampAssertion = new()
            }
        };
    }
}

/// <summary>
/// Managed wrapper around a native <see cref="C2paSettings"/> handle.
/// </summary>

public sealed partial class NativeSettings : IDisposable
{
    private readonly unsafe C2paSettings* handle;

    internal unsafe NativeSettings(C2paSettings* instance)
    {
        handle = instance;
    }

    public static unsafe implicit operator C2paSettings*(NativeSettings settings)
    {
        return settings.handle;
    }

    /// <summary>
    /// Allocates a new, empty native settings object.
    /// </summary>
    public static NativeSettings Create()
    {
        unsafe
        {
            var ptr = C2paBindings.settings_new();
            if (ptr == null)
                C2pa.CheckError();
            return new NativeSettings(ptr);
        }
    }

    /// <summary>
    /// Allocates a new native settings object from the supplied
    /// <see cref="Settings"/> DTO.
    /// </summary>
    public static NativeSettings From(C2pa.Settings settings)
    {
        var nativeSettings = Create();
        nativeSettings.UpdateFromString(settings.ToJson(indented: false));
        return nativeSettings;
    }

    /// <summary>
    /// Updates this settings object from a serialized representation.
    /// </summary>
    public void UpdateFromString(string contents, string format = "json")
    {
        unsafe
        {
            fixed (byte* s = Encoding.UTF8.GetBytes(contents))
            fixed (byte* f = Encoding.UTF8.GetBytes(format))
            {
                var ret = C2paBindings.settings_update_from_string(handle, (sbyte*)s, (sbyte*)f);
                if (ret != 0)
                    C2pa.CheckError();
            }
        }
    }

    /// <summary>
    /// Sets a single setting value by JSON Pointer / dotted path.
    /// </summary>
    public void SetValue(string path, string value)
    {
        unsafe
        {
            fixed (byte* p = Encoding.UTF8.GetBytes(path))
            fixed (byte* v = Encoding.UTF8.GetBytes(value))
            {
                var ret = C2paBindings.settings_set_value(handle, (sbyte*)p, (sbyte*)v);
                if (ret != 0)
                    C2pa.CheckError();
            }
        }
    }

    public void Dispose()
    {
        unsafe
        {
            C2paBindings.free(handle);
        }
    }

}