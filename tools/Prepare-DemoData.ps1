# PowerShell Demo Data Preparation Script
$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " [SystemCacheCleaner V1.0] Preparing Demo Data..." -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$demoBaseDir = Join-Path $env:LOCALAPPDATA "SystemCacheCleaner\DemoCache"

$categories = @(
    @{ Name = "UserTemp"; SubDir = "UserTemp"; Files = @(
        @{ Name = "user-cache-01.tmp"; Size = 1024 * 1024 * 2.5 },
        @{ Name = "user-temp-log.dat"; Size = 1024 * 512 },
        @{ Name = "user-draft-session.tmp"; Size = 1024 * 1024 * 1.2 }
    )},
    @{ Name = "WindowsTemp"; SubDir = "WindowsTemp"; Files = @(
        @{ Name = "win-update-chunk.tmp"; Size = 1024 * 1024 * 5.0 },
        @{ Name = "win-sys-diag.dat"; Size = 1024 * 256 }
    )},
    @{ Name = "EdgeCache"; SubDir = "EdgeCache"; Files = @(
        @{ Name = "edge-blob-001.cache"; Size = 1024 * 1024 * 3.1 },
        @{ Name = "edge-media-chunk.dat"; Size = 1024 * 1024 * 4.0 },
        @{ Name = "edge-favicon.tmp"; Size = 1024 * 128 }
    )}
)

$totalFilesCreated = 0
$totalBytesCreated = 0

foreach ($cat in $categories) {
    $targetDir = Join-Path $demoBaseDir $cat.SubDir
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    Write-Host "Category: [$($cat.Name)] -> $targetDir" -ForegroundColor Yellow

    foreach ($file in $cat.Files) {
        $filePath = Join-Path $targetDir $file.Name
        $sizeBytes = [long]$file.Size

        $buffer = New-Object byte[] $sizeBytes
        (New-Object Random).NextBytes($buffer)
        [System.IO.File]::WriteAllBytes($filePath, $buffer)

        $totalFilesCreated++
        $totalBytesCreated += $sizeBytes

        $sizeFormatted = "{0:N2} MB" -f ($sizeBytes / 1MB)
        Write-Host "   File: $($file.Name) ($sizeFormatted)" -ForegroundColor Green
    }
}

$totalSizeFormatted = "{0:N2} MB" -f ($totalBytesCreated / 1MB)

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "SUCCESS: Demo Data Ready!" -ForegroundColor Green
Write-Host "Target Directory: $demoBaseDir" -ForegroundColor White
Write-Host "Files Created: $totalFilesCreated" -ForegroundColor White
Write-Host "Total Size: $totalSizeFormatted" -ForegroundColor White
Write-Host "==================================================" -ForegroundColor Cyan
