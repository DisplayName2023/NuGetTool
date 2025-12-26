# NuGetTool

NuGetTool is a comprehensive utility for simplifying the creation and publishing of NuGet packages. It consists of a modern Web interface, a classic WPF application, and a shared core logic library.

## Project Components

- **NuGetTool.Web**: A Blazor-based web application providing a premium, cross-platform experience.
- **NuGetTool**: A classic Windows WPF GUI application.
- **NuGetTool.Core**: Shared class library containing business logic (Data models, spec generation, Pack/Push commands).

---

## Web Application (NuGetTool.Web)

The Web version is the primary tool for modern workflows, featuring dynamic configuration and automated metadata.

### How to Run
1. Navigate to: `NuGetTool.Web`
2. Run the server:
   ```powershell
   dotnet run
   ```
3. Access at `http://localhost:5000` (check terminal for exact URL).

### Key Features
- **Dynamic `nuget.config`**: Generates configuration on-the-fly for non-nuget.org sources (like GitLab).
- **Auto Metadata**: Extracts Package ID and Version automatically from uploaded files.
- **Automatic README**: Generates a `README.md` containing the file list if no description is provided, satisfying NuGet build requirements.
- **Credential Storage**: Load defaults from environment variables (`GITLAB_PACKAGE_REGISTRY_URL`, `GITLAB_PACKAGE_REGISTRY_USER`, etc.) or save them in local storage.


**defaults example**
```                        
export GITLAB_PACKAGE_REGISTRY_URL=http://gitlab.it.xxx.com/api/v4/projects/32/packages/nuget

export GITLAB_PACKAGE_REGISTRY_PASSWORD=xxxxxxxxxx-password
export GITLAB_PACKAGE_REGISTRY_USERNAME=xxxxxx-token-1
```


---

## WPF Application (NuGetTool)

A stable Windows-only application for desktop-centric package management.

### How to Run
1. Navigate to: `NuGetTool`
2. Run the application:
   ```powershell
   dotnet run
   ```

### Mode Selection
- **Nuspec Mode**: Uses `NuGet.exe` and a `.nuspec` file. Best for legacy or manual file control.
- **CSPROJ Mode**: Uses `dotnet` CLI and a dynamically generated `.csproj`. Best for modern workflows.

---

## Common Usage Steps

1. **Select Files**: Upload (Web) or Add (WPF) the files to be included.
2. **Review Metadata**:
   - **Package ID**: Auto-populated from the first file name.
   - **Version**: Auto-populated from file metadata or timestamps.
   - **Description**: If empty, defaults to a list of included files.
3. **Build Package**: Generates the build specification and runs `pack`.
4. **Upload Package**: Pushes to `nuget.org` using an API Key, or to private registries using dynamically generated credentials.
