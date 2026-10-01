param(
    [string]$SolutionDir = "D:\Tool Revit"
)

$ErrorActionPreference = "Continue"

Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host "   BIN TOOL - AUTOMATED REVIT ADDIN INTEGRITY & QUALITY AUDIT SUITE     " -ForegroundColor Cyan
Write-Host "=========================================================================" -ForegroundColor Cyan

$targets = @("net48", "net8.0-windows")
$totalErrors = 0
$totalWarnings = 0

foreach ($target in $targets) {
    Write-Host "`n[AUDITING TARGET: $target]" -ForegroundColor Yellow
    $srcDir = Join-Path $SolutionDir "src\$target\BIN"
    if (-not (Test-Path $srcDir)) {
        Write-Host "  [FAIL] Directory not found: $srcDir" -ForegroundColor Red
        $totalErrors++
        continue
    }

    # 1. Parse Panel.cs for registered buttons
    $panelFile = Join-Path $srcDir "Panel.cs"
    $panelContent = [System.IO.File]::ReadAllText($panelFile)
    
    $pushDataPattern = 'CreatePushData\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"'
    $btnMatches = [System.Text.RegularExpressions.Regex]::Matches($panelContent, $pushDataPattern)
    
    Write-Host "  Found $($btnMatches.Count) registered PushButtons in Panel.cs" -ForegroundColor Gray

    # 2. Check DevCommandProxy.cs
    $proxyFile = Join-Path $srcDir "DevCommandProxy.cs"
    $proxyLines = [System.IO.File]::ReadAllLines($proxyFile)

    foreach ($m in $btnMatches) {
        $btnName = $m.Groups[1].Value
        $cmdClass = $m.Groups[3].Value
        $imgName = $m.Groups[4].Value

        # Check Proxy Definition
        $expectedProxy = "DevProxy_$btnName"
        $matchingLine = $proxyLines | Where-Object { $_ -match "\bclass\s+$expectedProxy\b" }

        if (-not $matchingLine) {
            Write-Host "  [ERROR] Missing proxy class '$expectedProxy' in $target DevCommandProxy.cs!" -ForegroundColor Red
            $totalErrors++
        } else {
            # Check [Transaction(TransactionMode.Manual)] on the proxy class line or preceding line
            if (-not ($matchingLine -match '\[Transaction\(TransactionMode\.(?:Manual|ReadOnly)\)\]')) {
                Write-Host "  [ERROR] Proxy class '$expectedProxy' in $target is missing [Transaction] attribute!" -ForegroundColor Red
                $totalErrors++
            }
        }

        # Check Command Class file exists
        $foundClass = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Where-Object { 
            $content = [System.IO.File]::ReadAllText($_.FullName)
            $content -match "\bclass\s+$cmdClass\b"
        }
        if (-not $foundClass) {
            Write-Host "  [ERROR] Command implementation class '$cmdClass' not found in $target!" -ForegroundColor Red
            $totalErrors++
        } else {
            # Check [Transaction] on actual command class
            $classContent = [System.IO.File]::ReadAllText($foundClass[0].FullName)
            if (-not ($classContent -match "\[Transaction\(TransactionMode\.(?:Manual|ReadOnly)\)\]")) {
                Write-Host "  [ERROR] Command class '$cmdClass' in $($foundClass[0].Name) is missing [Transaction] attribute!" -ForegroundColor Red
                $totalErrors++
            }
        }
    }

    # 3. Check all IExternalCommand implementations in the directory
    $allCsFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse
    foreach ($cs in $allCsFiles) {
        $cText = [System.IO.File]::ReadAllText($cs.FullName)
        if ($cText -match "class\s+(\w+)\s*:\s*(?:[^\r\n]*,\s*)*IExternalCommand\b" -and -not ($cs.Name.Contains("DevCommandProxy"))) {
            $implName = $matches[1]
            if (-not ($cText -match "\[Transaction\(")) {
                Write-Host "  [ERROR] Class '$implName' in $($cs.Name) ($target) implements IExternalCommand but is missing [Transaction] attribute!" -ForegroundColor Red
                $totalErrors++
            }
        }
    }
}

Write-Host "`n=========================================================================" -ForegroundColor Cyan
if ($totalErrors -eq 0) {
    Write-Host "  AUDIT PASSED: 0 Errors, $totalWarnings Warning(s). Ready for deployment!" -ForegroundColor Green
    Write-Host "=========================================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "  AUDIT FAILED: $totalErrors Error(s), $totalWarnings Warning(s). Must fix before reporting!" -ForegroundColor Red
    Write-Host "=========================================================================" -ForegroundColor Cyan
    exit 1
}
