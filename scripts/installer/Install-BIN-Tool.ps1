$ErrorActionPreference = 'Continue'

Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host "            TIEN TRINH CAI DAT BIM TOOL (CURRENT USER)                  " -ForegroundColor Cyan
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host ""

# 0. Kiem tra xem Revit co dang mo khong
$revitProcesses = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revitProcesses) {
    Write-Host "[THONG BAO] Phat hien Revit dang mo tren may ($($revitProcesses.Count) tien trinh)!" -ForegroundColor Yellow
    Write-Host "-> Tu dong chuyen sang che do HOT-RELOAD LIVE (Khong tat Revit, giu nguyen mo hinh dang lam viec)..." -ForegroundColor Cyan
    $hotUpdateScript = Join-Path $PSScriptRoot "HotUpdate.ps1"
    if (Test-Path -LiteralPath $hotUpdateScript) {
        & $hotUpdateScript
        exit 0
    }
}

$sourceBundle = if (Test-Path (Join-Path $PSScriptRoot 'BIM.bundle')) { Join-Path $PSScriptRoot 'BIM.bundle' } else { Join-Path $PSScriptRoot 'BIN.bundle' }
$userToolRoot = Join-Path $env:APPDATA 'BIM TOOL'
$targetBundle = Join-Path $userToolRoot 'BIM.bundle'

# 1. Unblock tat ca cac file truoc tien
Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue

if (-not (Test-Path -LiteralPath $sourceBundle)) {
    Write-Host "[LOI] Khong tim thay thu muc bundle canh file cai dat!" -ForegroundColor Red
    Write-Host "Vui long giai nen toan bo file ZIP truoc khi chay Install.cmd." -ForegroundColor Yellow
    exit 1
}

New-Item -ItemType Directory -Path $userToolRoot -Force -ErrorAction SilentlyContinue | Out-Null

# 2. Backup hoac xoa ban cu an toan
if (Test-Path -LiteralPath $targetBundle) {
    try {
        $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
        $backupBundle = Join-Path $userToolRoot "BIM.bundle.backup_$timestamp"
        Move-Item -LiteralPath $targetBundle -Destination $backupBundle -Force -ErrorAction SilentlyContinue
        Write-Host "[OK] Da sao luu ban cu sang: $backupBundle" -ForegroundColor DarkGray
    } catch {
        Write-Host "[CHU Y] Khong the di chuyen ban cu, dang ghi de truc tiep..." -ForegroundColor DarkYellow
    }
}

# 3. Copy BIM.bundle moi vao AppData
try {
    Copy-Item -LiteralPath $sourceBundle -Destination $targetBundle -Recurse -Force
    Get-ChildItem -LiteralPath $targetBundle -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
    Write-Host "[OK] Da sao chep du lieu BIM.bundle vao AppData thanh cong." -ForegroundColor Green
} catch {
    Write-Host "[LOI] Khong the ghi de vao AppData: $_" -ForegroundColor Red
    Write-Host "Vui long dong tat ca cac cua so Revit roi chay lai Install.cmd!" -ForegroundColor Yellow
    exit 1
}

# 4. Khoi tao cau hinh Hot-Reload san sang cho cac lan cap nhat sau
$devDir1 = Join-Path $env:LOCALAPPDATA 'BIM TOOL\Development'
$devDir2 = Join-Path $env:LOCALAPPDATA 'BIN TOOL\Development'
New-Item -ItemType Directory -Path $devDir1 -Force -ErrorAction SilentlyContinue | Out-Null
New-Item -ItemType Directory -Path $devDir2 -Force -ErrorAction SilentlyContinue | Out-Null
$installedDllNet48 = Join-Path $targetBundle "Contents\net48\BIN.dll"
$installedDllNet8 = Join-Path $targetBundle "Contents\net8.0-windows\BIN.dll"
if (Test-Path -LiteralPath $installedDllNet48) {
    Set-Content -LiteralPath (Join-Path $devDir1 "hotreload-net48.path") -Value $installedDllNet48 -Encoding UTF8 -Force
    Set-Content -LiteralPath (Join-Path $devDir2 "hotreload-net48.path") -Value $installedDllNet48 -Encoding UTF8 -Force
}
if (Test-Path -LiteralPath $installedDllNet8) {
    Set-Content -LiteralPath (Join-Path $devDir1 "hotreload-net8.path") -Value $installedDllNet8 -Encoding UTF8 -Force
    Set-Content -LiteralPath (Join-Path $devDir2 "hotreload-net8.path") -Value $installedDllNet8 -Encoding UTF8 -Force
}
Write-Host "[OK] Da thiet lap cau hinh Hot-Reload san sang." -ForegroundColor Green


# 5. Tim phien ban Revit da cai tren may
$detectedVersions = @()
$commonVersions = 2020..2026

foreach ($v in $commonVersions) {
    $found = $false
    if ((Test-Path "C:\Program Files\Autodesk\Revit $v\Revit.exe") -or 
        (Test-Path "D:\Program Files\Autodesk\Revit $v\Revit.exe") -or
        (Test-Path (Join-Path $env:APPDATA "Autodesk\Revit\Addins\$v"))) {
        $found = $true
    }
    if ($found) {
        $detectedVersions += $v
    }
}

if ($detectedVersions.Count -eq 0) {
    Write-Host "[INFO] Tu dong dang ky Addin cho tat ca phien ban Revit tu 2020 den 2026." -ForegroundColor Cyan
    $targetVersions = $commonVersions
} else {
    Write-Host "[OK] Phat hien cac phien ban Revit tren may: $($detectedVersions -join ', ')" -ForegroundColor Cyan
    $targetVersions = $detectedVersions
}

# 6. Dang ky file BIM.addin vao cac thu muc Addins & XOA SACH BIN.addin CU
$installedCount = 0
foreach ($version in $targetVersions) {
    $runtimeFolder = if ($version -le 2024) { 'net48' } else { 'net8.0-windows' }
    $dllPath = Join-Path $targetBundle "Contents\$runtimeFolder\BIN.dll"
    if (-not (Test-Path $dllPath)) {
        continue
    }
    $escapedDllPath = [System.Security.SecurityElement]::Escape($dllPath)
    $addinFolder = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
    
    # XOA TRIET DE FILE BIN.addin CU (TRONG CA USER APPDATA VA PROGRAMDATA)
    $oldBinAddin = Join-Path $addinFolder 'BIN.addin'
    if (Test-Path -LiteralPath $oldBinAddin) {
        Remove-Item -LiteralPath $oldBinAddin -Force -ErrorAction SilentlyContinue
        Write-Host "  -> Da xoa sach file BIN.addin cu tren Revit $version" -ForegroundColor DarkGray
    }
    $progDataAddin = Join-Path ${env:ProgramData} "Autodesk\Revit\Addins\$version\BIN.addin"
    if (Test-Path -LiteralPath $progDataAddin) {
        try {
            Remove-Item -LiteralPath $progDataAddin -Force -ErrorAction SilentlyContinue
            Write-Host "  -> Da xoa file BIN.addin cu trong ProgramData Revit $version" -ForegroundColor DarkGray
        } catch {}
    }
    
    $addinPath = Join-Path $addinFolder 'BIM.addin'
    
    try {
        New-Item -ItemType Directory -Path $addinFolder -Force -ErrorAction SilentlyContinue | Out-Null
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
        Write-Host "  -> Da dang ky thanh cong BIM.addin cho Revit $version" -ForegroundColor Green
        $installedCount++
    } catch {
        Write-Host "  -> [LOI] Khong the dang ky cho Revit $version - $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=========================================================================" -ForegroundColor Green
Write-Host "   CAI DAT BIM TOOL THANH CONG! ($installedCount phien ban da duoc kich hoat)" -ForegroundColor Green
Write-Host "=========================================================================" -ForegroundColor Green
Write-Host "Vi tri cai dat: $targetBundle" -ForegroundColor Gray
Write-Host "Huong dan:" -ForegroundColor Yellow
Write-Host "1. Mo Revit len." -ForegroundColor Yellow
Write-Host "2. Neu Revit hoi xac nhan add-in lan dau, hay chon 'Always Load'." -ForegroundColor Yellow
Write-Host "3. Tren thanh Ribbon se xuat hien tab 'BIM - MEP' va 'BIM - DOCS'." -ForegroundColor Yellow
Write-Host ""
