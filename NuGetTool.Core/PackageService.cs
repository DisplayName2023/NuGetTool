using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NuGetTool.Core;

public class PackageService
{
    public void GenerateNuspec(PackageMetadata data, string outputPath)
    {
        string workingDir = Path.GetDirectoryName(outputPath) ?? "";
        string readmePath = Path.Combine(workingDir, "README.md");
        string description = GetDescription(data);
        
        File.WriteAllText(readmePath, description);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<package>");
        sb.AppendLine("  <metadata>");
        sb.AppendLine($"    <id>{data.Id}</id>");
        sb.AppendLine($"    <version>{data.Version}</version>");
        sb.AppendLine($"    <authors>{(string.IsNullOrWhiteSpace(data.Authors) ? "Unknown" : data.Authors)}</authors>");
        sb.AppendLine($"    <description>{description}</description>");
        sb.AppendLine("    <readme>README.md</readme>");
        sb.AppendLine("    <contentFiles>");
        foreach (var file in data.ContentFiles)
        {
            string fileName = Path.GetFileName(file);
            sb.AppendLine($"      <files include=\"any\\any\\{fileName}\" buildAction=\"None\" copyToOutput=\"true\" flatten=\"true\" />");
        }
        sb.AppendLine("    </contentFiles>");
        sb.AppendLine("  </metadata>");
        sb.AppendLine("  <files>");
        sb.AppendLine("    <file src=\"README.md\" target=\"\" />");
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
        string workingDir = Path.GetDirectoryName(outputPath) ?? "";
        string readmePath = Path.Combine(workingDir, "README.md");
        string description = GetDescription(data);

        File.WriteAllText(readmePath, description);

        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
        sb.AppendLine($"    <PackageId>{data.Id}</PackageId>");
        sb.AppendLine($"    <Version>{data.Version}</Version>");
        sb.AppendLine($"    <Authors>{(string.IsNullOrWhiteSpace(data.Authors) ? "Unknown" : data.Authors)}</Authors>");
        sb.AppendLine($"    <Description>{description}</Description>");
        sb.AppendLine("    <PackageReadmeFile>README.md</PackageReadmeFile>");
        sb.AppendLine("    <IncludeContentInPack>true</IncludeContentInPack>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("  <ItemGroup>");
        sb.AppendLine("    <None Include=\"README.md\" Pack=\"true\" PackagePath=\"\\\" />");
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

    private string GetDescription(PackageMetadata data)
    {
        if (!string.IsNullOrWhiteSpace(data.Description))
            return data.Description;

        if (data.ContentFiles.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {data.Id}");
            sb.AppendLine();
            sb.AppendLine("Package containing the following files:");
            sb.AppendLine();
            foreach (var file in data.ContentFiles)
            {
                sb.AppendLine($"- {Path.GetFileName(file)}");
            }
            return sb.ToString();
        }

        return "No description provided.";
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
            // If we have a custom config file (likely with credentials), don't pass ApiKey to avoid conflicts
            string authArg = (!string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(configContent)) ? $"-ApiKey {apiKey}" : "";
            string configArg = !string.IsNullOrEmpty(configFile) ? $"-ConfigFile \"{configFile}\"" : "";

            RunCommand(nugetExePath, $"push \"{packagePath}\" {authArg} {sourceArg} {configArg}", workingDir);
        }
        else
        {
             // Dotnet nuget push
            string sourceArg = string.IsNullOrEmpty(source) ? "" : $"--source \"{source}\"";
            // If we have a custom config file (likely with credentials), don't pass ApiKey to avoid conflicts
            string authArg = (!string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(configContent)) ? $"--api-key {apiKey}" : "";
            
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
    
    public string GetNuGetConfigContent(string sourceKey, string sourceUrl, string username, string password)
    {
        return $$$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
             <packageSources>
                 <clear />
                 <add key="{{{sourceKey}}}" value="{{{sourceUrl}}}" allowInsecureConnections="true"/>
             </packageSources>
             <packageSourceCredentials>
                 <{{{sourceKey}}}>
                     <add key="Username" value="{{{username}}}" />
                     <add key="ClearTextPassword" value="{{{password}}}" />
                 </{{{sourceKey}}}>
             </packageSourceCredentials>
            </configuration>
            """;
    }

    private void RunCommand(string fileName, string arguments, string? workingDir = null)
    {
        OnLog?.Invoke($"Running: {fileName} {arguments}");
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
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
