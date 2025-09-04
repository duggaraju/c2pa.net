// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Tests;

public class C2paTests
{
    [Fact]
    public void Version_ShouldReturnNonEmptyString()
    {
        // Act
        var version = C2pa.Version;

        // Assert
        Assert.NotNull(version);
        const string Version = "0.61.0";

        Assert.Equal($"c2pa-c-ffi/{Version} c2pa-rs/{Version}", version);
    }

    [Fact]
    public void SupportedMimeTypes_ShouldReturnNonEmptyArray()
    {
        // Act
        var mimeTypes = C2pa.SupportedMimeTypes;

        // Assert
        Assert.NotNull(mimeTypes);
        Assert.NotEmpty(mimeTypes);
        Assert.Contains("image/jpeg", mimeTypes);
    }

    [Fact(Skip = "Flaky")]
    public void CheckError_ShouldNotThrowWhenNoError()
    {
        // Act & Assert
        var exception = Record.Exception(() => C2pa.CheckError());
        Assert.Null(exception);
    }
}