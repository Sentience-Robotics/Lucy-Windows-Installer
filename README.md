# Lucy Windows Installer

This repository contains the Windows installer for [Lucy](https://github.com/Sentience-Robotics/lucy_ws).

The installer:

- Clones the Lucy workspace from GitHub.
- Supports selecting a branch or release tag.
- Installs `pixi` automatically when it is not already available.
- Runs the configured setup commands.
- Publishes as a self-contained Windows executable.

## Configuring installer commands

Add or change the commands run after the Lucy repository is cloned near the top of:

```text
MainWindow.xaml.cs
```

The commands are defined in the `COMMANDS` list:

```csharp
private static readonly List<string> COMMANDS = new()
{
    "pixi install",
    "pixi run build"
};
```

Commands are executed in order from inside the cloned Lucy workspace.

## Creating a release

The GitHub Actions workflow runs when a version tag beginning with `v` is pushed. It builds the installer and attaches the raw `Lucy.exe` file to a GitHub Release.

Create and push a release tag with:

```powershell
git add .
git commit -sm "Prepare release v1.0.0"
git push origin master

git tag v1.0.0
git push origin v1.0.0
```

Use a new version for each release, for example `v1.0.1` or `v2.0.0`. Pushing the tag starts the workflow, creates the release if necessary, and uploads `Lucy.exe` to the release.

## Building locally

Build the project with .NET 8:

```powershell
dotnet build .\Lucy-windows-installer.csproj
```

To create a release-style self-contained executable:

```powershell
dotnet publish .\Lucy-windows-installer.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  --output publish
```

## License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.

See [LICENSE](LICENSE) for the full license text.