using Microsoft.Win32;
using NuGetTool.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace NuGetTool;

public partial class MainWindow : Window
{
    private ObservableCollection<string> _contentFiles = new();
    private const string NuGetExePath = @"..\..\DownloadNuget\NuGet.exe"; 
    private readonly PackageService _packageService;

    public MainWindow()
    {
        InitializeComponent();
        lstFiles.ItemsSource = _contentFiles;
        _packageService = new PackageService();
        _packageService.OnLog += msg => Dispatcher.Invoke(() => Log(msg));
        this.Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log($"Ready. Working directory: {Directory.GetCurrentDirectory()}");
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isNuspec = rbNuspec.IsChecked == true;
        
        if (btnGenerateNuspec != null) btnGenerateNuspec.Visibility = isNuspec ? Visibility.Visible : Visibility.Collapsed;
        if (btnGenerateCsproj != null) btnGenerateCsproj.Visibility = isNuspec ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Multiselect = true };
        if (dlg.ShowDialog() == true)
        {
            foreach (var file in dlg.FileNames)
            {
                if (!_contentFiles.Contains(file))
                {
                    _contentFiles.Add(file);
                }
            }
            UpdateDefaultsFromFirstFile();
        }
    }

    private void LstFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var file in files)
            {
                if (File.Exists(file) && !_contentFiles.Contains(file))
                {
                    _contentFiles.Add(file);
                }
            }
            UpdateDefaultsFromFirstFile();
        }
    }

    private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
    {
        _contentFiles.Clear();
    }

    private void UpdateDefaultsFromFirstFile()
    {
        if (_contentFiles.Count > 0)
        {
            var firstFile = _contentFiles[0];
            var info = new FileInfo(firstFile);
            
            if (string.IsNullOrWhiteSpace(txtPackageId.Text))
            {
                txtPackageId.Text = Path.GetFileNameWithoutExtension(info.Name);
            }
            if (string.IsNullOrWhiteSpace(txtVersion.Text))
            {
                txtVersion.Text = info.LastWriteTime.ToString("yyyy.MM.dd");
            }
        }
    }

    private PackageMetadata GetMetadata()
    {
        return new PackageMetadata
        {
            Id = txtPackageId.Text,
            Version = txtVersion.Text,
            Authors = txtAuthors.Text,
            Description = txtDescription.Text,
            ContentFiles = new List<string>(_contentFiles)
        };
    }

    private void BtnGenerateNuspec_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs()) return;
        try 
        {
            _packageService.GenerateNuspec(GetMetadata(), "Package.nuspec");
            Log("Generated Package.nuspec");
        }
        catch (Exception ex)
        {
            Log($"Error generating nuspec: {ex.Message}");
        }
    }

    private void BtnGenerateCsproj_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs()) return;
         try 
        {
            _packageService.GenerateCsproj(GetMetadata(), "Package.csproj");
            Log("Generated Package.csproj");
        }
        catch (Exception ex)
        {
            Log($"Error generating csproj: {ex.Message}");
        }
    }

    private async void BtnBuild_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs()) return;
        bool isNuspec = rbNuspec.IsChecked == true;
        
        await Task.Run(() => 
        {
            try
            {
                var meta = Dispatcher.Invoke(GetMetadata);
                
                if (isNuspec)
                {
                     if (!File.Exists("Package.nuspec"))
                        _packageService.GenerateNuspec(meta, "Package.nuspec");
                     
                     string nugetPath = Path.GetFullPath(NuGetExePath);
                     _packageService.BuildPackage("Package.nuspec", true, nugetPath);
                }
                else
                {
                     if (!File.Exists("Package.csproj"))
                        _packageService.GenerateCsproj(meta, "Package.csproj");

                     _packageService.BuildPackage("Package.csproj", false);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"Error building: {ex.Message}"));
            }
        });
    }

    private async void BtnUpload_Click(object sender, RoutedEventArgs e)
    {
         bool isNuspec = rbNuspec.IsChecked == true;
         string id = txtPackageId.Text;
         string version = txtVersion.Text;
         
         await Task.Run(() => 
         {
            try 
            {
                string nupkg = $"{id}.{version}.nupkg";
                 // If not found, look for any nupkg matching ID
                if (!File.Exists(nupkg))
                {
                     var files = Directory.GetFiles(Directory.GetCurrentDirectory(), $"{id}*.nupkg");
                     if (files.Length > 0) nupkg = files[0];
                     else 
                     {
                         var binFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), $"{id}*.nupkg", SearchOption.AllDirectories);
                         if (binFiles.Length > 0) nupkg = binFiles[0];
                     }
                }

                string nugetPath = Path.GetFullPath(NuGetExePath);
                _packageService.UploadPackage(nupkg, isNuspec, "gitlab", nugetPath);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"Error uploading: {ex.Message}"));
            }
         });
    }

    private bool ValidateInputs()
    {
         if (string.IsNullOrWhiteSpace(txtPackageId.Text)) { MessageBox.Show("Package ID is required"); return false; }
         if (string.IsNullOrWhiteSpace(txtVersion.Text)) { MessageBox.Show("Version is required"); return false; }
         return true;
    }

    private void Log(string message)
    {
        txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        txtLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        txtLog.Clear();
    }
}