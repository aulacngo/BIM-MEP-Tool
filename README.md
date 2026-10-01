# BIM-MEP-Tool

**BIM-MEP-Tool** is an Autodesk Revit MEP automation add-in for faster, more consistent MEP modeling and documentation workflows.

## Platform support

| Autodesk Revit | Target framework |
| --- | --- |
| 2020 - 2024 | .NET Framework 4.8 (`net48`) |
| 2025 - 2026 | .NET 8.0 Windows (`net8.0-windows`) |

## Features

The add-in provides **75 automation tools** across two ribbon tabs:

- `BIM - MEP`
- `BIM - DOCS`

The commands are organized into ten focused panels:

1. Micro
2. MEP
3. Sprinkler
4. Drainage
5. Support
6. Check
7. BOQ & Excel
8. Sheet & View
9. 2D Annotation
10. Transfer & Link

## Build

Use PowerShell from the repository root. The development script runs the add-in integrity audit and builds the selected target in `Release` mode.

```powershell
# Build both targets without changing installed Revit manifests/loaders
.\Dev-BIN.ps1 -SkipLoader

# Build one target only
.\Dev-BIN.ps1 -Runtime net48 -SkipLoader
.\Dev-BIN.ps1 -Runtime net8 -SkipLoader
```

By default, `Dev-BIN.ps1` also stages a development loader and updates installed Revit add-in manifests. Use that default only for a deliberate local hot-reload workflow.

## Package

After successful Release builds, create the current-user portable distribution with:

```powershell
.\Package-BIN.ps1
```

The packaging script creates a dated ZIP under `release/`, includes the Revit bundle and installer, and maps the compiled add-in to the supported Revit framework:

- Revit 2020 - 2024: `Contents\net48\BIN.dll`
- Revit 2025 - 2026: `Contents\net8.0-windows\BIN.dll`

## Repository layout

```text
src/net48/           Revit 2020-2024 add-in source
src/net8.0-windows/  Revit 2025-2026 add-in source
scripts/             Installer, validation, and release helpers
Dev-BIN.ps1          Development build and optional hot-reload workflow
Package-BIN.ps1      Portable release packaging workflow
```

## Notes

Build output, packaged releases, installer binaries, Revit test models, local credentials, and IDE-specific files are excluded from version control. Package and test the correct target in a live Revit model before release.
