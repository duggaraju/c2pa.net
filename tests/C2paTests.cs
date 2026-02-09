// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

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
        var expectedVersion = GetWorkspacePackageVersion();

        Assert.Equal($"c2pa-c-ffi/{expectedVersion} c2pa-rs/{expectedVersion}", version);
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

    private static string GetWorkspacePackageVersion()
    {
        var repoRoot = FindRepoRoot();
        var cargoTomlPath = Path.Combine(repoRoot, "c2pa-rs", "Cargo.toml");

        var inWorkspacePackage = false;

        foreach (var rawLine in File.ReadLines(cargoTomlPath))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inWorkspacePackage = line.Equals("[workspace.package]", StringComparison.Ordinal);
                continue;
            }

            if (!inWorkspacePackage || line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var match = Regex.Match(line, "^version\\s*=\\s*\"(?<version>[^\"]+)\"");
            if (match.Success)
            {
                return match.Groups["version"].Value;
            }
        }

        throw new InvalidOperationException("Could not find [workspace.package] version in Cargo.toml.");
    }

    private static string FindRepoRoot()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --show-toplevel",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start git process.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git rev-parse failed: {error}");
        }

        var repoRoot = output.Trim();
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new InvalidOperationException("git rev-parse returned empty repo root.");
        }

        return repoRoot;
    }
}