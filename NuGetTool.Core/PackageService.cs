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
        sb.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
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
        string workingDir = Path.GetDirectoryName(projectPath) ?? "";
        if (isNuspec)
        {
            if (string.IsNullOrEmpty(nugetExePath) || !File.Exists(nugetExePath))
            {
                // Fallback to "nuget" in PATH if not provided or valid
                nugetExePath = "nuget"; 
            }
            RunCommand(nugetExePath, $"pack \"{projectPath}\"", workingDir);
        }
        else
        {
            RunCommand("dotnet", $"pack \"{projectPath}\"", workingDir);
        }
    }

    public void UploadPackage(string packagePath, bool isNuspec, string source, string? apiKey = null, string? configContent = null, string? nugetExePath = null)
    {
        if (!File.Exists(packagePath))
            throw new FileNotFoundException($"Package not found: {packagePath}");

        string workingDir = Path.GetDirectoryName(packagePath) ?? "";
        string configFile = "";

        if (!string.IsNullOrEmpty(configContent))
        {
            configFile = Path.Combine(workingDir, "nuget.config");
            File.WriteAllText(configFile, configContent);
        }

        if (isNuspec)
        {
             if (string.IsNullOrEmpty(nugetExePath) || !File.Exists(nugetExePath))
            {
                nugetExePath = "nuget";
            }
            
            string sourceArg = string.IsNullOrEmpty(source) ? "" : $"-Source \"{source}\"";
            string authArg = !string.IsNullOrEmpty(apiKey) ? $"-ApiKey {apiKey}" : "";
            string configArg = !string.IsNullOrEmpty(configFile) ? $"-ConfigFile \"{configFile}\"" : "";

            RunCommand(nugetExePath, $"push \"{packagePath}\" {authArg} {sourceArg} {configArg}", workingDir);
        }
        else
        {
             // Dotnet nuget push
            string sourceArg = string.IsNullOrEmpty(source) ? "" : $"--source \"{source}\"";
            string authArg = !string.IsNullOrEmpty(apiKey) ? $"--api-key {apiKey}" : "";
            
            // For dotnet, we can use the config file via --configfile or it will be picked up if it's named nuget.config in the current dir
            // However, to be explicit:
            string configArg = !string.IsNullOrEmpty(configFile) ? $"--configfile \"{configFile}\"" : "";

            RunCommand("dotnet", $"nuget push \"{packagePath}\" {authArg} {sourceArg} {configArg}", workingDir);
        }
    }

    private Process? _currentProcess;

    public void CancelOperation()
    {
        try
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                OnLog?.Invoke("Cancelling operation...");
                _currentProcess.Kill(true);
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Error while cancelling: {ex.Message}");
        }
    }

    public event Action<string>? OnLog;

    private void RunCommand(string fileName, string arguments, string? workingDir = null)
    {
        OnLog?.Invoke($"Running: {fileName} {arguments}");
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c chcp 65001 >nul && {fileName} {arguments}",
            WorkingDirectory = workingDir ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _currentProcess = new Process { StartInfo = startInfo };
        _currentProcess.OutputDataReceived += (s, e) => { if (e.Data != null) OnLog?.Invoke(e.Data); };
        _currentProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) OnLog?.Invoke(e.Data); };

        try
        {
            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();
            _currentProcess.WaitForExit();
        }
        finally
        {
            _currentProcess.Dispose();
            _currentProcess = null;
        }
    }
}
