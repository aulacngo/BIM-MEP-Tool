param(
    [ValidateSet("All", "net48", "net8")]
    [string]$Runtime = "All",
    [switch]$InstallLoader = $true,
    [switch]$SkipLoader,
    [switch]$Diagnostics,
    [switch]$Journal,
    [switch]$SkipBuild
)

if ($SkipLoader) { $InstallLoader = $false }

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$developmentDir = Join-Path $env:LOCALAPPDATA "BIN TOOL\Development"
$installedBundle = Join-Path $env:APPDATA "BIN TOOL\BIN.bundle\Contents"
$targets = @()

if ($Runtime -eq "All" -or $Runtime -eq "net48") {
    $targets += [pscustomobject]@{
        Name = "net48"
        Project = Join-Path $root "src\net48\BIN.csproj"
        Output = Join-Path $root "src\net48\bin\Release\net48"
        Config = Join-Path $developmentDir "hotreload-net48.path"
        Install = Join-Path $installedBundle "net48"
    }
}

if ($Runtime -eq "All" -or $Runtime -eq "net8") {
    $targets += [pscustomobject]@{
        Name = "net8"
        Project = Join-Path $root "src\net8.0-windows\BIN.csproj"
        Output = Join-Path $root "src\net8.0-windows\bin\Release\net8.0-windows"
        Config = Join-Path $developmentDir "hotreload-net8.path"
        Install = Join-Path $installedBundle "net8.0-windows"
    }
}

if ($Diagnostics -or $Journal) {
    $endpoint = if ($Journal) { "journal/latest?n=400" } else { "commands/latest?n=100" }
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:8077/$endpoint" -TimeoutSec 5
        $result | ConvertTo-Json -Depth 10
    }
    catch {
        throw "Cannot access MCP at localhost:8077. Open Revit and verify that BIN TOOL is loaded. $($_.Exception.Message)"
    }
    exit 0
}

if (-not $SkipBuild) {
    # 0. Automated Add-in Quality & Integrity Audit
    $auditScript = Join-Path $root "Test-BinAddinAudit.ps1"
    if (Test-Path $auditScript) {
        & powershell -ExecutionPolicy Bypass -File $auditScript -SolutionDir $root
        if ($LASTEXITCODE -ne 0) {
            throw "Add-in Integrity Audit failed! Resolve errors before building."
        }
    }

    foreach ($target in $targets) {
        & dotnet build $target.Project -c Release --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build $($target.Name) failed."
        }
    }
}

if (-not (Test-Path -LiteralPath $developmentDir)) {
    New-Item -ItemType Directory -Path $developmentDir | Out-Null
}

foreach ($target in $targets) {
    $dll = Join-Path $target.Output "BIN.dll"
    if (-not (Test-Path -LiteralPath $dll)) {
        throw "Built DLL was not found: $dll"
    }

    [System.IO.File]::WriteAllText($target.Config, $dll)
    Write-Host "Hot reload $($target.Name): $dll"

    if ($InstallLoader) {
        if (-not (Test-Path -LiteralPath $target.Install)) {
            throw "Installed bundle was not found at $($target.Install). Install BIN TOOL first."
        }

        $loaderName = "BIN.DevLoader.$(Get-Date -Format 'yyyyMMddHHmmss').dll"
        $loaderPath = Join-Path $target.Install $loaderName
        Copy-Item -LiteralPath $dll -Destination $loaderPath -Force

        $pdb = Join-Path $target.Output "BIN.pdb"
        if (Test-Path -LiteralPath $pdb) {
            Copy-Item -LiteralPath $pdb -Destination ([System.IO.Path]::ChangeExtension($loaderPath, ".pdb")) -Force
        }

        $target | Add-Member -NotePropertyName LoaderPath -NotePropertyValue $loaderPath -Force
        Write-Host "Staged $($target.Name) loader at $loaderPath"
    }
}

if ($InstallLoader) {
    $manifestRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins"
    $manifests = @(Get-ChildItem -LiteralPath $manifestRoot -Include @("BIN.addin", "BIM.addin") -Recurse -File -ErrorAction SilentlyContinue)
    if ($manifests.Count -eq 0) {
        throw "No installed BIM.addin or BIN.addin manifest was found under $manifestRoot"
    }

    foreach ($manifest in $manifests) {
        $version = 0
        [void][int]::TryParse($manifest.Directory.Name, [ref]$version)
        $runtimeName = if ($version -ge 2025) { "net8" } else { "net48" }
        $target = $targets | Where-Object { $_.Name -eq $runtimeName } | Select-Object -First 1
        if ($null -eq $target -or [string]::IsNullOrWhiteSpace($target.LoaderPath)) {
            continue
        }

        [xml]$xml = Get-Content -LiteralPath $manifest.FullName
        $assemblyNode = $xml.SelectSingleNode("/RevitAddIns/AddIn/Assembly")
        if ($null -eq $assemblyNode) {
            throw "Assembly node was not found in $($manifest.FullName)"
        }
        $assemblyNode.InnerText = [string]$target.LoaderPath
        $xml.Save($manifest.FullName)
        Write-Host "Updated Revit $version manifest: $($target.LoaderPath)"
    }

    Write-Host "Loader installation completed. Restart Revit once."
}
else {
    Write-Host "Build completed. Ribbon commands will use the new DLL on the next click without restarting Revit."
}

Write-Host "Diagnostics: powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Diagnostics"
Write-Host "Journal:     powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Journal"
