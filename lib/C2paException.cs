// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

[Serializable]
public class C2paException(string type, string message) : Exception(message)
{
    public readonly string Type = type;
}