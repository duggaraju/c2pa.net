// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ContentAuthenticity.Tests;

public class C2paTests
{
    [Fact]
    public void ManagedWrappers_ShouldCoverCurrentNativeApisWithoutExposingDeprecatedApis()
    {
        var repoRoot = FindRepoRoot();
        var nativeSourceRoot = Path.Combine(repoRoot, "c2pa-rs", "c2pa_c_ffi", "src");
        var nativeSources = Directory.EnumerateFiles(nativeSourceRoot, "*.rs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var deprecatedExports = nativeSources
            .SelectMany(source => Regex.Matches(source,
                "#\\[deprecated\\b[^\\]]*\\](?:\\s*#\\[[^\\]]*\\])*\\s*pub\\s+(?:unsafe\\s+)?(?:extern\\s+\"C\"\\s+)?fn\\s+(?<name>c2pa_\\w+)"))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(deprecatedExports);

        var nativeExports = nativeSources
            .SelectMany(source => Regex.Matches(source,
                "pub\\s+(?:unsafe\\s+)?extern\\s+\"C\"\\s+fn\\s+(?<name>c2pa_\\w+)"))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(nativeExports);

        var bindings = typeof(C2paBindings).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => (method.Name, Import: method.GetCustomAttribute<DllImportAttribute>()))
            .Where(binding => binding.Import != null)
            .ToArray();
        var missingBindings = nativeExports.Except(deprecatedExports)
            .Except(bindings.Select(binding => binding.Import!.EntryPoint))
            .Order()
            .ToArray();
        Assert.True(missingBindings.Length == 0,
            $"Regenerate bindings for new native exports: {string.Join(", ", missingBindings)}");

        var deprecatedBindings = bindings
            .Where(binding => deprecatedExports.Contains(binding.Import!.EntryPoint!))
            .Select(binding => binding.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(deprecatedBindings.Count == 0,
            $"Exclude deprecated native APIs from binding generation: {string.Join(", ", deprecatedBindings)}");

        var libraryRoot = Path.Combine(repoRoot, "lib");
        var calls = Directory.EnumerateFiles(libraryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(libraryRoot, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(part => part is "Bindings" or "bin" or "obj"))
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path), @"\bC2paBindings\s*\.\s*(?<name>\w+)\s*\(")
                .Select(match => (Path: Path.GetRelativePath(repoRoot, path), Name: match.Groups["name"].Value)))
            .ToArray();
        var violations = calls.Where(call => deprecatedExports.Contains($"c2pa_{call.Name}"))
            .Select(call => $"{call.Path}: {call.Name}")
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Managed wrappers call deprecated native APIs:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");

        var missingWrappers = bindings.Select(binding => binding.Name)
            .Except(deprecatedBindings)
            .Except(calls.Select(call => call.Name))
            .Order()
            .ToArray();
        Assert.True(missingWrappers.Length == 0,
            $"Add managed wrappers for native APIs: {string.Join(", ", missingWrappers)}");
    }

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
        var exception = Record.Exception(() =>
        {
            _ = C2pa.Version;
            C2pa.CheckError();
        });
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