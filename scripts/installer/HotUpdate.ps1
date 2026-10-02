$ErrorActionPreference = 'Continue'

Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host "       BIM TOOL - HOT RELOAD (CAP NHAT KHONG CAN TAT REVIT)              " -ForegroundColor Cyan
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host ""

$sourceBundle = if (Test-Path (Join-Path $PSScriptRoot 'BIM.bundle')) { Join-Path $PSScriptRoot 'BIM.bundle' } else { Join-Path $PSScriptRoot 'BIN.bundle' }
$userToolRoot = Join-Path $env:APPDATA 'BIM TOOL'
$targetBundle = Join-Path $userToolRoot 'BIM.bundle'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'

# 1. Unblock tat ca file
Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue

if (-not (Test-Path -LiteralPath $sourceBundle)) {
    Write-Host "[LOI] Khong tim thay thu muc BIM.bundle!" -ForegroundColor Red
    Write-Host "Vui long giai nen toan bo file ZIP truoc khi chay HotUpdate.cmd." -ForegroundColor Yellow
    exit 1
}

# 2. Dam bao thu muc dich ton tai
New-Item -ItemType Directory -Path $userToolRoot -Force -ErrorAction SilentlyContinue | Out-Null
$devDir1 = Join-Path $env:LOCALAPPDATA 'BIM TOOL\Development'
$devDir2 = Join-Path $env:LOCALAPPDATA 'BIN TOOL\Development'
New-Item -ItemType Directory -Path $devDir1 -Force -ErrorAction SilentlyContinue | Out-Null
New-Item -ItemType Directory -Path $devDir2 -Force -ErrorAction SilentlyContinue | Out-Null

# 3. Kiem tra Revit dang mo
$revitProcesses = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revitProcesses) {
    Write-Host "[OK] Phat hien Revit dang chay ($($revitProcesses.Count) tien trinh)." -ForegroundColor Green
    Write-Host "-> Kich hoat Hot-Reload: KHONG TAT REVIT, nap code moi truc tiep vao phien lam viec!" -ForegroundColor Yellow
} else {
    Write-Host "[INFO] Revit chua mo. Da cau hinh san Hot-Reload de san sang khi khoi dong Revit." -ForegroundColor Cyan
}

# 4. Copy Shadow DLLs (Khong bao gio bi Windows/Revit khoa file)
$net48Source = Join-Path $sourceBundle "Contents\net48\BIN.dll"
$net8Source = Join-Path $sourceBundle "Contents\net8.0-windows\BIN.dll"

$targetNet48Folder = Join-Path $targetBundle "Contents\net48"
$targetNet8Folder = Join-Path $targetBundle "Contents\net8.0-windows"

New-Item -ItemType Directory -Path $targetNet48Folder -Force -ErrorAction SilentlyContinue | Out-Null
New-Item -ItemType Directory -Path $targetNet8Folder -Force -ErrorAction SilentlyContinue | Out-Null

$hotDllNet48 = Join-Path $targetNet48Folder "BIN.HotReload.$timestamp.dll"
$hotDllNet8 = Join-Path $targetNet8Folder "BIN.HotReload.$timestamp.dll"

if (Test-Path -LiteralPath $net48Source) {
    Copy-Item -LiteralPath $net48Source -Destination $hotDllNet48 -Force
    Unblock-File -LiteralPath $hotDllNet48 -ErrorAction SilentlyContinue
    
    # Cap nhat file config hotreload-net48.path
    Set-Content -LiteralPath (Join-Path $devDir1 "hotreload-net48.path") -Value $hotDllNet48 -Encoding UTF8 -Force
    Set-Content -LiteralPath (Join-Path $devDir2 "hotreload-net48.path") -Value $hotDllNet48 -Encoding UTF8 -Force
    Write-Host "[OK] Da nap DLL Hot-Reload .NET 4.8 (Revit 2020-2024): BIN.HotReload.$timestamp.dll" -ForegroundColor Green
}

if (Test-Path -LiteralPath $net8Source) {
    Copy-Item -LiteralPath $net8Source -Destination $hotDllNet8 -Force
    Unblock-File -LiteralPath $hotDllNet8 -ErrorAction SilentlyContinue
    
    # Cap nhat file config hotreload-net8.path
    Set-Content -LiteralPath (Join-Path $devDir1 "hotreload-net8.path") -Value $hotDllNet8 -Encoding UTF8 -Force
    Set-Content -LiteralPath (Join-Path $devDir2 "hotreload-net8.path") -Value $hotDllNet8 -Encoding UTF8 -Force
    Write-Host "[OK] Da nap DLL Hot-Reload .NET 8.0 (Revit 2025+): BIN.HotReload.$timestamp.dll" -ForegroundColor Green
}

# 5. Dong bo cac thu muc Resources (Icons, Tooltips, ...) neu can
$resSource = Join-Path $sourceBundle "Contents\Resources"
$resDest = Join-Path $targetBundle "Contents\Resources"
if (Test-Path -LiteralPath $resSource) {
    New-Item -ItemType Directory -Path $resDest -Force -ErrorAction SilentlyContinue | Out-Null
    Copy-Item -LiteralPath "$resSource\*" -Destination $resDest -Recurse -Force -ErrorAction SilentlyContinue
}

# 6. Dam bao BIM.addin da duoc dang ky tren tat ca phien ban Revit tren may
$detectedVersions = @()
$commonVersions = 2020..2026
foreach ($v in $commonVersions) {
    if ((Test-Path "C:\Program Files\Autodesk\Revit $v\Revit.exe") -or 
        (Test-Path "D:\Program Files\Autodesk\Revit $v\Revit.exe") -or
        (Test-Path (Join-Path $env:APPDATA "Autodesk\Revit\Addins\$v"))) {
        $detectedVersions += $v
    }
}
$targetVersions = if ($detectedVersions.Count -gt 0) { $detectedVersions } else { $commonVersions }

foreach ($version in $targetVersions) {
    $runtimeFolder = if ($version -le 2024) { 'net48' } else { 'net8.0-windows' }
    $dllPath = Join-Path $targetBundle "Contents\$runtimeFolder\BIN.dll"
    
    if (-not (Test-Path $dllPath)) {
        $sourceDll = if ($version -le 2024) { $hotDllNet48 } else { $hotDllNet8 }
        if (Test-Path $sourceDll) {
            Copy-Item -LiteralPath $sourceDll -Destination $dllPath -Force -ErrorAction SilentlyContinue
        }
    }
    
    $addinFolder = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
    $addinPath = Join-Path $addinFolder 'BIM.addin'
    if (-not (Test-Path -LiteralPath $addinPath)) {
        try {
            New-Item -ItemType Directory -Path $addinFolder -Force -ErrorAction SilentlyContinue | Out-Null
            $escapedDllPath = [System.Security.SecurityElement]::Escape($dllPath)
            $manifest = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM TOOL</Name>
    <Assembly>$escapedDllPath</Assembly>
    <AddInId>782C5A9C-B0C7-40EE-8258-A08A2D5448DE</AddInId>
    <FullClassName>BIN.Panel</FullClassName>
    <VendorId>Create By BIM Team</VendorId>
    <VendorDescription>lephuocbinhbk@gmail.com</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
            Set-Content -LiteralPath $addinPath -Value $manifest -Encoding UTF8 -Force
            Write-Host "  -> Dang ky BIM.addin cho Revit $version" -ForegroundColor DarkGray
        } catch {}
    }
}

Write-Host ""
Write-Host "=========================================================================" -ForegroundColor Green
Write-Host "   HOT-RELOAD THANH CONG! (CAP NHAT LIVE KHONG CAN TAT REVIT)           " -ForegroundColor Green
Write-Host "=========================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Huong dan su dung:" -ForegroundColor Yellow
Write-Host "1. Ban KHONG CAN tat Revit va KHONG CAN load lai mo hinh." -ForegroundColor Yellow
Write-Host "2. Hay quay lai cua so Revit dang mo." -ForegroundColor Yellow
Write-Host "3. Bam vao nut bat ky tren tab 'BIM - MEP' de chay ngay tinh nang moi nhat!" -ForegroundColor Yellow
Write-Host ""
