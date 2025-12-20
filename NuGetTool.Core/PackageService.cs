using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NuGetTool.Core;

public class PackageService
{
    public void GenerateNuspec(PackageMetadata data, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<package>");
        sb.AppendLine("  <metadata>");
        sb.AppendLine($"    <id>{data.Id}</id>");
        sb.AppendLine($"    <version>{data.Version}</version>");
        sb.AppendLine($"    <authors>{(string.IsNullOrWhiteSpace(data.Authors) ? "Unknown" : data.Authors)}</authors>");
        sb.AppendLine($"    <description>{(string.IsNullOrWhiteSpace(data.Description) ? "No description" : data.Description)}</description>");
        sb.AppendLine("    <contentFiles>");
        foreach (var file in data.ContentFiles)
        {
            string fileName = Path.GetFileName(file);
            sb.AppendLine($"      <files include=\"any\\any\\{fileName}\" buildAction=\"None\" copyToOutput=\"true\" flatten=\"true\" />");
        }
        sb.AppendLine("    </contentFiles>");
        sb.AppendLine("  </metadata>");
        sb.AppendLine("  <files>");
        foreach (var file in data.ContentFiles)
        {
            string fileName = Path.GetFileName(file);
            sb.AppendLine($"    <file src=\"{file}\" target=\"contentFiles\\any\\any\\{fileName}\" />");
        }
        sb.AppendLine("  </files>");
        sb.AppendLine("</package>");
        File.WriteAllText(outputPath, sb.ToString());
    }

    public void GenerateCsproj(PackageMetadata data, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
        sb.AppendLine($"    <PackageId>{data.Id}</PackageId>");
        sb.AppendLine($"    <Version>{data.Version}</Version>");
        sb.AppendLine($"    <Authors>{(string.IsNullOrWhiteSpace(data.Authors) ? "Unknown" : data.Authors)}</Authors>");
        sb.AppendLine($"    <Description>{(string.IsNullOrWhiteSpace(data.Description) ? "No description" : data.Description)}</Description>");
        sb.AppendLine("    <IncludeContentInPack>true</IncludeContentInPack>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("  <ItemGroup>");
        foreach (var file in data.ContentFiles)
        {
            string fileName = Path.GetFileName(file);
            sb.AppendLine($"    <Content Include=\"{file}\" Pack=\"true\" PackagePath=\"contentFiles/any/any/{fileName}\">");
            sb.AppendLine("       <PackageCopyToOutput>true</PackageCopyToOutput>");
            sb.AppendLine("    </Content>");
        }
        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine("</Project>");
        File.WriteAllText(outputPath, sb.ToString());
    }

    public void BuildPackage(string projectPath, bool isNuspec, string? nugetExePath = null)
    {
        if (isNuspec)
        {
            if (string.IsNullOrEmpty(nugetExePath) || !File.Exists(nugetExePath))
            {
                // Fallback to "nuget" in PATH if not provided or valid
                nugetExePath = "nuget"; 
            }
            RunCommand(nugetExePath, $"pack \"{projectPath}\"");
        }
        else
        {
            RunCommand("dotnet", $"pack \"{projectPath}\"");
        }
    }

    public void UploadPackage(string packagePath, bool isNuspec, string source, string? nugetExePath = null)
    {
        if (!File.Exists(packagePath))
            throw new FileNotFoundException($"Package not found: {packagePath}");

        if (isNuspec)
        {
             if (string.IsNullOrEmpty(nugetExePath) || !File.Exists(nugetExePath))
            {
                nugetExePath = "nuget";
            }
            // Note: Source handling might need more flexibility, passing explicit source for now
            string sourceArg = string.IsNullOrEmpty(source) ? "" : $"-Source {source}";
            RunCommand(nugetExePath, $"push \"{packagePath}\" {sourceArg}");
        }
        else
        {
             // Dotnet nuget push
            string sourceArg = string.IsNullOrEmpty(source) ? "" : $"--source {source}";
            RunCommand("dotnet", $"nuget push \"{packagePath}\" {sourceArg}");
        }
    }

    public event Action<string>? OnLog;

    private void RunCommand(string fileName, string arguments)
    {
        OnLog?.Invoke($"Running: {fileName} {arguments}");
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (s, e) => { if (e.Data != null) OnLog?.Invoke(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) OnLog?.Invoke(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(); 
    }
}
