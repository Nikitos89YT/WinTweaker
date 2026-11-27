# Run with: powershell.exe -NoProfile -File tests/TabLayout.Smoke.ps1
$ErrorActionPreference = 'Stop'

function Get-PageButtons($name) {
    $file = Join-Path $PSScriptRoot "../WinTweaker/Pages/$name.xaml"
    $page = [xml][IO.File]::ReadAllText($file)
    $page.SelectNodes('//*[local-name()="Button"][@Click]')
}

$tweaks = @(Get-PageButtons 'TweaksPage')
$tools = @(Get-PageButtons 'ToolsPage')
$drivers = @(Get-PageButtons 'DriverManagerPage')
$failures = @()
foreach ($handler in 'GameModeButton_Click', 'StretchButton_Click', 'RestoreResolutionButton_Click', 'NetworkAdapterToggleButton_Click', 'ApplyDnsButton_Click', 'FlushDnsButton_Click', 'ResetWinsockButton_Click', 'ResetTcpIpButton_Click', 'RefreshNetworkButton_Click') {
    if (@($tweaks | Where-Object Click -eq $handler).Count -ne 1 -or @($tools | Where-Object Click -eq $handler).Count -ne 0) {
        $failures += "Expected one $handler button only in Tweaks."
    }
}
foreach ($handler in 'CheckDiskButton_Click', 'RepairDiskButton_Click', 'SfcButton_Click', 'DismCheckHealthButton_Click', 'DismScanHealthButton_Click', 'DismRestoreHealthButton_Click', 'FullRepairButton_Click', 'CleanUserTempButton_Click', 'CleanWindowsTempButton_Click', 'CleanDirectXShaderCacheButton_Click', 'CleanThumbnailCacheButton_Click', 'CleanIconCacheButton_Click', 'CleanDeliveryOptimizationButton_Click', 'EmptyRecycleBinButton_Click', 'SafeCleanupButton_Click', 'RestartExplorerButton_Click', 'OpenServicesButton_Click') {
    if (@($tools | Where-Object Click -eq $handler).Count -ne 1 -or @($tweaks | Where-Object Click -eq $handler).Count -ne 0) {
        $failures += "Expected one $handler button only in Tools."
    }
}
$both = $tools + $tweaks
foreach ($duplicate in 'CleanSystemIconCacheButton_Click', 'CleanSystemThumbnailCacheButton_Click') {
    if ($both | Where-Object Click -eq $duplicate) { $failures += "Duplicate cleanup button: $duplicate." }
}
if ($both | Where-Object { $_.GetAttribute('Tag') -eq 'Services' }) { $failures += 'Duplicate Windows Services shortcut.' }
$shortcuts = @($tools | Where-Object Click -eq 'LaunchSystemComponentButton_Click' | ForEach-Object { $_.GetAttribute('Tag') })
foreach ($tag in 'TaskManager', 'DiskManagement', 'EventViewer', 'ResourceMonitor', 'SystemProperties', 'ControlPanel', 'NetworkConnections') {
    if (@($shortcuts | Where-Object { $_ -eq $tag }).Count -ne 1) { $failures += "Expected one $tag shortcut in Tools." }
}
if ($tweaks | Where-Object Click -eq 'LaunchSystemComponentButton_Click') { $failures += 'Found a Windows shortcut in Tweaks.' }
if (@($tools | Where-Object Click -eq 'OpenDriverManagerButton_Click').Count -ne 1) { $failures += 'Expected one Driver Manager entry in Tools.' }
if ($both | Where-Object { $_.GetAttribute('Tag') -eq 'DeviceManager' -or $_.GetAttribute('Click') -eq 'OpenDeviceManagerButton_Click' }) { $failures += 'Device Manager shortcut must not be in Tools or Tweaks.' }
foreach ($handler in 'OpenDeviceManagerButton_Click', 'ExportOemDriversButton_Click', 'BackToToolsButton_Click') {
    if (@($drivers | Where-Object Click -eq $handler).Count -ne 1) { $failures += "Expected one $handler button in Driver Manager." }
}
if ($failures.Count) { throw ($failures -join "`n") }
Write-Output 'PASS Driver Manager: entry, return, export and Device Manager shortcuts'
Write-Output 'PASS Tweaks: game, display and network actions'
Write-Output 'PASS Tools: diagnostics, individual and batch cleanup, Explorer and Windows shortcuts'
