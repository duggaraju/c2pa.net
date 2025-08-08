// See https://aka.ms/new-console-template for more information
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Passes;
using System.Diagnostics;
using System.Runtime.InteropServices;

var library = new Library();
ConsoleDriver.Run(library);
Console.WriteLine("Generated C# output!");

class Library : ILibrary
{
    public static string GetRootDirectory()
    {
        // use git rev-parse --show-toplevel to get the root directory
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --show-toplevel",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    public void Postprocess(Driver driver, ASTContext ctx)
    {
    }

    public void Preprocess(Driver driver, ASTContext ctx)
    {
    }

    public void Setup(Driver driver)
    {
        var baseDir = GetRootDirectory();
        Console.WriteLine("Base Dir is {0}", baseDir);
        var options = driver.Options;
        options.GeneratorKind = GeneratorKind.CSharp;
        options.Encoding = System.Text.Encoding.UTF8;
        options.OutputDir = Path.Combine(baseDir, "lib", "generated");
        options.CheckSymbols = false;
        var module = options.AddModule("C2paBindings");
        var path = Path.Combine(baseDir, "c2pa-rs", "target",
#if DEBUG        
            "debug");
#else
            "release");
#endif
        module.IncludeDirs.Add(path);
        module.Headers.Add("c2pa.h");
        module.LibraryDirs.Add(path);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            module.Libraries.Add("c2pa_c.dll");
        else
            module.Libraries.Add("libc2pa_c.so");
        module.OutputNamespace = "ContentAuthenticity.Bindings";
    }

    public void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.RenameDeclsUpperCase(RenameTargets.Any);
        //driver.Context.TranslationUnitPasses.AddPass(new FunctionToInstanceMethodPass());
    }
}