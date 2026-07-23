Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$demoRoot = Join-Path $env:LOCALAPPDATA 'SystemCacheCleaner\DemoCache\UserTemp'
$demoFile = Join-Path $demoRoot 'codex-m06-m07-review.tmp'
$exe = Resolve-Path 'src\SystemCacheCleaner\bin\Debug\net8.0-windows\SystemCacheCleaner.exe'

function Find-Window([string]$name, [int]$attempts = 50) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $name)

    for ($i = 0; $i -lt $attempts; $i++) {
        $window = $root.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $condition)
        if ($null -ne $window) {
            return $window
        }
        Start-Sleep -Milliseconds 100
    }

    $windowNames = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition) |
        ForEach-Object { $_.Current.Name } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    throw "窗口未出现: $name。当前窗口: $($windowNames -join ' | ')"
}

function Find-Button($window, [string]$name) {
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $name)
    $condition = New-Object System.Windows.Automation.AndCondition(
        $typeCondition,
        $nameCondition)

    return $window.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
}

function Invoke-Button($window, [string]$name) {
    $button = Find-Button $window $name
    if ($null -eq $button) {
        throw "未找到按钮: $name"
    }
    $pattern = $button.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

New-Item -ItemType Directory -Path $demoRoot -Force | Out-Null
[System.IO.File]::WriteAllBytes($demoFile, (New-Object byte[] 32768))
$process = Start-Process -FilePath $exe -ArgumentList '--demo' -PassThru

try {
    $main = Find-Window '系统缓存清理工具软件 V1.0'
    Invoke-Button $main '开始扫描'

    $cleanButton = $null
    for ($i = 0; $i -lt 100; $i++) {
        Start-Sleep -Milliseconds 100
        $cleanButton = Find-Button $main '立即清理'
        if ($null -ne $cleanButton -and $cleanButton.Current.IsEnabled) {
            break
        }
    }
    if ($null -eq $cleanButton -or -not $cleanButton.Current.IsEnabled) {
        throw '扫描未完成或清理按钮未启用'
    }

    $cleanPattern = $cleanButton.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $cleanPattern.Invoke()

    $confirmation = Find-Window '确认执行缓存清理'
    Invoke-Button $confirmation '确认清理'

    $report = Find-Window '清理报告' 100
    $textCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $texts = $report.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $textCondition)
    $timeText = $texts |
        ForEach-Object { $_.Current.Name } |
        Where-Object { $_ -like '开始：*结束：*' } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($timeText)) {
        throw '报告未显示开始/结束时间'
    }

    $rect = $report.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap(
        [int]$rect.Width,
        [int]$rect.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen(
            [int]$rect.X,
            [int]$rect.Y,
            0,
            0,
            $bitmap.Size)
        $screenshot = Join-Path (Resolve-Path 'artifacts\m06') 'cleanup-report.png'
        $bitmap.Save($screenshot, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    "REPORT_TIME=$timeText"
    "SCREENSHOT=$screenshot"
    Invoke-Button $report '关闭'
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 300
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }

    if (Test-Path -LiteralPath $demoFile) {
        Remove-Item -LiteralPath $demoFile -Force
    }
    "DEMO_FILE_REMOVED=$(-not (Test-Path -LiteralPath $demoFile))"
}
