using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;

namespace NuGetTool.Web.Services;

public class MetadataService
{
    public async Task<(string? Id, string? Version)> ExtractMetadataAsync(IBrowserFile browserFile, string savedPath)
    {
        string? id = Path.GetFileNameWithoutExtension(browserFile.Name);
        string? version = null;

        // 1. Try to extract version from binary if it's a DLL or EXE
        if (browserFile.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
            browserFile.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try 
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(savedPath);
                version = versionInfo.ProductVersion ?? versionInfo.FileVersion;
            }
            catch { }
        }

        // 2. Try to extract from Xilinx PDI if applicable
        if (string.IsNullOrWhiteSpace(version) && browserFile.Name.EndsWith(".pdi", StringComparison.OrdinalIgnoreCase))
        {
            version = await TryExtractPdiDateAsync(savedPath);
        }

        // 3. Fallback to Browser's LastModified date if version is still empty
        if (string.IsNullOrWhiteSpace(version))
        {
            version = browserFile.LastModified.ToString("yyyy.MM.dd");
        }

        return (id, version);
    }

    private async Task<string?> TryExtractPdiDateAsync(string filePath)
    {
        try
        {
            // Xilinx PDI headers often have strings like "Nov 18 2020" or "2024.1"
            // We'll scan the first 16KB for potential date strings
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[16384];
            int read = await fs.ReadAsync(buffer, 0, buffer.Length);
            
            // Convert to string (ASCII/Latin1 as headers are usually text-based metadata)
            string content = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
            
            // Look for patterns like "Nov 18 2020" or "yyyy-MM-dd"
            // This is a heuristic but can be useful for Xilinx files
            var datePatterns = new[] {
                @"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2}\s+\d{4}\b", // Nov 18 2020
                @"\b\d{4}\.\d{1,2}\.\d{1,2}\b", // 2024.01.01
                @"\b\d{4}-\d{2}-\d{2}\b" // 2024-01-01
            };

            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(content, pattern);
                if (match.Success)
                {
                    // Try to parse it to a standard yyyy.MM.dd format
                    if (DateTime.TryParse(match.Value, out var dt))
                    {
                        return dt.ToString("yyyy.MM.dd");
                    }
                }
            }
        }
        catch { }

        return null;
    }
}
