$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class StockWindow { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd); }
'@
$process = Start-Process -FilePath "$PWD/artifacts/windows/SiteSyncTracker.exe" -PassThru
try {
    $window = $null
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Seconds 2
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) { break }
        if ($process.HasExited) { throw "Windows application exited before a preview could be captured." }
    }
    if (-not $window) { throw "Windows application window did not appear." }
    [StockWindow]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Seconds 3
    function Save-WindowPreview($name) {
        $bounds = $window.Current.BoundingRectangle
        if ($bounds.Width -lt 100 -or $bounds.Height -lt 100) { throw "Application window has no visible bounds." }
        $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
            $bitmap.Save("$PWD/$name", [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    Save-WindowPreview "windows-dashboard.png"
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)))
    $button = @($buttons | Where-Object { $_.Current.Name -match "Search Manifest|Search inventory" }) | Select-Object -First 1
    if (-not $button) { throw "Inventory navigation was not found." }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $textCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,"Ready. Saved changes stay in the workspace shown above.")
    $stock = $null
    for ($attempt=0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Seconds 2
        $stock = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$textCondition)
        if ($stock) { break }
    }
    if (-not $stock) {
        Save-WindowPreview "windows-after-navigation.png"
        $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { Write-Host ("UI: " + $_.Current.Name) }
        throw "Windows stock workspace did not appear."
    }
    Save-WindowPreview "windows-stock.png"
    Write-Host "Windows stock workspace rendered; preview captured."
} finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force } }
