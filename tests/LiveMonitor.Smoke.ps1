$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '../WinTweaker'
$homeXml = [xml][IO.File]::ReadAllText((Join-Path $root 'Pages/HomePage.xaml'))
if (!$homeXml.SelectSingleNode('//*[local-name()="Button"][@Click="ShowOverlay_Click"]')) { throw 'Home does not offer Live Monitor.' }
$overlay = [xml][IO.File]::ReadAllText((Join-Path $root 'LiveMonitorWindow.xaml'))
if ($overlay.DocumentElement.Topmost -ne 'True' -or $overlay.DocumentElement.ShowActivated -ne 'False') { throw 'Overlay must open on top without stealing focus.' }
foreach ($handler in 'Graphs_Click','Fps_Click','Lock_Click','Close_Click') {
    if (!$overlay.SelectSingleNode("//*[@Click='$handler']")) { throw "Overlay has no $handler control." }
}
'PASS Live Monitor entry point and overlay controls'
