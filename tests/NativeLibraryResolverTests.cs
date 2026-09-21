// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using ContentAuthenticity.Bindings;
using System.Runtime.InteropServices;

namespace ContentAuthenticity.Tests;

public class NativeLibraryResolverTests
{
    [Theory]
    [InlineData("linux-musl-x64", "LINUX", Architecture.X64, false, new[] { "linux-musl-x64" })]
    [InlineData("linux-musl-x64", "LINUX", Architecture.X64, true, new[] { "linux-musl-x64" })]
    [InlineData("linux-musl-arm64", "LINUX", Architecture.Arm64, false, new[] { "linux-musl-arm64" })]
    [InlineData("alpine.3.22-x64", "LINUX", Architecture.X64, true, new[] { "alpine.3.22-x64", "linux-musl-x64" })]
    [InlineData("alpine.3.22-arm64", "LINUX", Architecture.Arm64, true, new[] { "alpine.3.22-arm64", "linux-musl-arm64" })]
    [InlineData("linux-x64", "LINUX", Architecture.X64, false, new[] { "linux-x64" })]
    [InlineData("linux-arm64", "LINUX", Architecture.Arm64, false, new[] { "linux-arm64" })]
    [InlineData("ubuntu.24.04-x64", "LINUX", Architecture.X64, false, new[] { "ubuntu.24.04-x64", "linux-x64" })]
    [InlineData("win-x64", "WINDOWS", Architecture.X64, false, new[] { "win-x64" })]
    [InlineData("win10-arm64", "WINDOWS", Architecture.Arm64, false, new[] { "win10-arm64", "win-arm64" })]
    [InlineData("osx-arm64", "OSX", Architecture.Arm64, false, new[] { "osx-arm64" })]
    [InlineData("osx.14-x64", "OSX", Architecture.X64, false, new[] { "osx.14-x64", "osx-x64" })]
    [InlineData("linux-musl-arm", "LINUX", Architecture.Arm, false, new[] { "linux-musl-arm" })]
    public void CandidateRids_PreserveLibcAndArchitecture(
        string runtimeRid, string platform, Architecture architecture, bool isAlpine, string[] expected)
    {
        Assert.Equal(expected, NativeLibraryResolver.GetCandidateRids(
            runtimeRid, OSPlatform.Create(platform), architecture, isAlpine));
    }
}