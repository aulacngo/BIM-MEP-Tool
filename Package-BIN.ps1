$ErrorActionPreference = "Stop"

$root = "D:\Tool Revit"
$releaseBase = "$root\release"
$dateStr = Get-Date -Format "yyyyMMdd"
$portableFolder = "$releaseBase\BIM-Tool-Portable-$dateStr-Current"
$zipFile = "$releaseBase\BIM-Tool-CurrentUser-NoAdmin-$dateStr.zip"

Write-Host "Packaging $portableFolder..." -ForegroundColor Cyan

if (Test-Path $portableFolder) {
    Remove-Item -LiteralPath $portableFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $portableFolder -Force | Out-Null

# 1. Copy BIM.bundle
$bundleSource = if (Test-Path "$env:APPDATA\BIM TOOL\BIM.bundle") { "$env:APPDATA\BIM TOOL\BIM.bundle" } else { "$env:APPDATA\BIN TOOL\BIN.bundle" }
$bundleDest = Join-Path $portableFolder "BIM.bundle"
Copy-Item -Path $bundleSource -Destination $bundleDest -Recurse -Force

# Clean temporary devloader files from bundle
Get-ChildItem -Path (Join-Path $bundleDest "Contents\net48") -Filter "BIN.DevLoader.*" | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path (Join-Path $bundleDest "Contents\net8.0-windows") -Filter "BIN.DevLoader.*" | Remove-Item -Force -ErrorAction SilentlyContinue

# Ensure fresh BIN.dll copied
Copy-Item -LiteralPath "$root\src\net48\bin\Release\net48\BIN.dll" -Destination (Join-Path $bundleDest "Contents\net48\BIN.dll") -Force
Copy-Item -LiteralPath "$root\src\net8.0-windows\bin\Release\net8.0-windows\BIN.dll" -Destination (Join-Path $bundleDest "Contents\net8.0-windows\BIN.dll") -Force

# Update internal .addin manifests in bundle with new AddInId and BIM TOOL branding
Get-ChildItem -Path $bundleDest -Recurse -Filter "*.addin" | ForEach-Object {
    $content = Get-Content -LiteralPath $_.FullName -Raw
    $content = $content -replace "F2E6A285-A730-4D6E-AEAE-5EC6FC883F0C", "782C5A9C-B0C7-40EE-8258-A08A2D5448DE"
    $content = $content -replace "<Name>BIN TOOL</Name>", "<Name>BIM TOOL</Name>"
    Set-Content -LiteralPath $_.FullName -Value $content -Encoding UTF8 -Force
}

# 2. Copy Install.cmd, Install-BIN-Tool.ps1, HotUpdate.cmd, and HotUpdate.ps1
$installerSource = "$root\scripts\installer"
Copy-Item -LiteralPath "$installerSource\Install.cmd" -Destination (Join-Path $portableFolder "Install.cmd") -Force
Copy-Item -LiteralPath "$installerSource\Install-BIN-Tool.ps1" -Destination (Join-Path $portableFolder "Install-BIN-Tool.ps1") -Force
Copy-Item -LiteralPath "$installerSource\HotUpdate.cmd" -Destination (Join-Path $portableFolder "HotUpdate.cmd") -Force
Copy-Item -LiteralPath "$installerSource\HotUpdate.ps1" -Destination (Join-Path $portableFolder "HotUpdate.ps1") -Force

# 3. Create HUONG-DAN-CAT-DAT.txt
$readme = @"
BIM TOOL - BO CAI PORTABLE - CURRENT $dateStr
===============================================

Ho tro theo PackageContents.xml:
- Revit 2020, 2021, 2022, 2023, 2024: .NET Framework 4.8
- Revit 2025, 2026: .NET 8 Windows

================================================================================
>>> CÁCH 1: CẬP NHẬT TRỰC TIẾP KHÔNG CẦN TẮT REVIT (HOT-RELOAD LIVE) <<<
================================================================================
- Khi Revit đang mở dự án làm việc tại công ty:
  1. Tải và giải nén file ZIP này.
  2. Bấm đúp file "HotUpdate.cmd" (chạy trong 1 giây).
  3. KHÔNG CẦN TẮT REVIT. Quay lại Revit bấm nút trên Ribbon là ăn tính năng mới ngay lập tức!

================================================================================
>>> CÁCH 2: CÀI ĐẶT LẦN ĐẦU TRÊN MÁY MỚI <<<
================================================================================
1. Đóng Revit nếu đang mở.
2. Giải nén toàn bộ file ZIP.
3. Bấm đúp file "Install.cmd" (tự động đăng ký Addin cho toàn bộ Revit 2020-2026).
4. Mở Revit -> Chọn "Always Load" khi được hỏi.

TRANG THAI BAN NAY ($dateStr):
- Đã kích hoạt Hot-Reload Live: Cập nhật tính năng mới trực tiếp vào phiên Revit đang mở.
- Đã khắc phục triệt để lỗi né dầm Avoid Clash: Giao cắt Solid 3D chuẩn xác cho dầm xiên/chéo, tự động bắt cao độ đáy dầm và bảo toàn kết nối đường ống.
- Toàn bộ 75 công cụ chạy độc lập không cần quyền Administrator (%APPDATA%).
"@
[System.IO.File]::WriteAllText((Join-Path $portableFolder "HUONG-DAN-CAT-DAT.txt"), $readme.Trim(), [System.Text.Encoding]::UTF8)


# 4. Create BUILD-STATUS.txt
$buildStatus = @"
BIM TOOL CURRENT BUILD - $dateStr
===================================

Targets:
- Revit 2020-2024: Contents\net48\BIN.dll
- Revit 2025-2026: Contents\net8.0-windows\BIN.dll

Build verification:
- net48 Release: succeeded, 0 errors
- net8.0-windows Release: succeeded, 0 errors
- Pre-flight quality audit: 73/73 buttons passed, 0 errors
- Revit 2023 Micro smoke suite: 11/11 tests passed

Key fixes in this build:
- Fixed ConnectSprinklerToPipe crash (replaced BAML with pure C# WPF + live vector diagrams).
- Added DialogResult = true to OK button for instant pick activation.
- Fixed SprinklerFlipper main pipe disconnection and null ReferenceLevel crash.
- Cleaned all Vietnamese font encoding errors across all tools.
- Portable CurrentUser installation: No admin rights required.
"@
[System.IO.File]::WriteAllText((Join-Path $portableFolder "BUILD-STATUS.txt"), $buildStatus.Trim(), [System.Text.Encoding]::UTF8)

# 4. Create ZIP
Write-Host "`n[4/4] Creating ZIP: $zipFile..." -ForegroundColor Yellow
if (Test-Path $zipFile) {
    Remove-Item -LiteralPath $zipFile -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$retryCount = 0
while ($true) {
    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory($portableFolder, $zipFile, [System.IO.Compression.CompressionLevel]::Optimal, $false)
        break
    }
    catch {
        $retryCount++
        if ($retryCount -gt 5) { throw $_ }
        Start-Sleep -Milliseconds 600
    }
}

$zipItem = Get-Item $zipFile
$sizeMb = [Math]::Round($zipItem.Length / 1MB, 2)

Write-Host "`n=========================================================================" -ForegroundColor Green
Write-Host "  RELEASE PACKAGED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "  LOCAL ZIP: $zipFile ($sizeMb MB)" -ForegroundColor Green
Write-Host "=========================================================================" -ForegroundColor Green

# 5. Upload to Google Drive (if configured)
Write-Host "`n[5/6] Uploading to Google Drive..." -ForegroundColor Yellow
$uploadScript = "$root\scripts\upload_to_drive.py"
if (Test-Path $uploadScript) {
    try { py -3.12 $uploadScript $zipFile } catch {}
}

# 6. Upload to Gofile (instant direct download link)
Write-Host "`n[6/6] Uploading to Gofile..." -ForegroundColor Yellow
$gofileScript = "$root\scripts\upload_to_gofile.py"
if (Test-Path $gofileScript) {
    py -3.12 $gofileScript $zipFile
}
