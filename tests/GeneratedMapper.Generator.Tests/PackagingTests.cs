using System.Diagnostics;
using System.IO.Compression;

namespace GeneratedMapper.Generator.Tests;

// buildTransitive/GeneratedMapper.Generator.props auto-configures InterceptorsNamespaces for any
// real NuGet consumer with zero manual .csproj edits. This codifies that as a repeatable
// regression test: nothing else would catch, say, someone reorganizing
// GeneratedMapper.Generator.csproj and silently dropping the pack item for the props file.
public class PackagingTests
{
    [Fact]
    public void Pack_IncludesBuildTransitivePropsFile_SettingInterceptorsNamespaces()
    {
        var repoRoot = FindRepoRoot();
        var csprojPath = Path.Combine(repoRoot, "src", "GeneratedMapper.Generator", "GeneratedMapper.Generator.csproj");
        var workDir = Path.Combine(Path.GetTempPath(), "GeneratedMapper.Generator.PackagingTest." + Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(workDir, "out");
        Directory.CreateDirectory(outDir);

        try
        {
            // /nodeReuse:false: this runs `dotnet pack` from *inside* an already-running
            // `dotnet test` process, and the shared MSBuild/VBCSCompiler build-server node being
            // contended by both at once reproducibly hangs without this flag. Only the package
            // *output* is isolated (-o) - also isolating BaseIntermediateOutputPath/BaseOutputPath
            // broke restore for the ProjectReference to GeneratedMapper.Abstractions instead.
            var psi = new ProcessStartInfo(
                "dotnet",
                $"pack \"{csprojPath}\" -o \"{outDir}\" -c Release --nologo /nodeReuse:false")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            var exited = process.WaitForExit(180_000);

            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort cleanup */ }
            }

            Assert.True(exited, "dotnet pack did not complete within the timeout.");
            Assert.True(process.ExitCode == 0, $"dotnet pack failed (exit {process.ExitCode}).\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

            var nupkgPath = Directory.GetFiles(outDir, "GeneratedMapper.Generator.*.nupkg").SingleOrDefault();
            Assert.False(string.IsNullOrEmpty(nupkgPath), $"No GeneratedMapper.Generator .nupkg produced in {outDir}.\nSTDOUT:\n{stdout}");

            using var archive = ZipFile.OpenRead(nupkgPath!);

            var propsEntry = archive.GetEntry("buildTransitive/GeneratedMapper.Generator.props");
            Assert.NotNull(propsEntry);

            using var reader = new StreamReader(propsEntry!.Open());
            var content = reader.ReadToEnd();
            Assert.Contains("InterceptorsNamespaces", content);
            Assert.Contains("GeneratedMapper", content);

            // The analyzer payload itself must still be present too - this test should catch a
            // regression in either piece, not just the new props file.
            Assert.NotNull(archive.GetEntry("analyzers/dotnet/cs/GeneratedMapper.Generator.dll"));
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GeneratedMapper.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate GeneratedMapper.sln by walking up from " + AppContext.BaseDirectory);
        }

        return dir.FullName;
    }
}
