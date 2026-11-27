# Checks the WPF navigation contract without running Windows recovery.
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '../WinTweaker'
$main = [xml][IO.File]::ReadAllText((Join-Path $root 'MainWindow.xaml'))
$link = $main.SelectSingleNode('//*[local-name()="Button" and @Click="RestoreCenterButton_Click"]')
if (!$link) { throw 'Restore Center navigation is missing.' }
$page = [xml][IO.File]::ReadAllText((Join-Path $root 'Pages/RestoreCenterPage.xaml'))
foreach ($handler in 'CreatePoint_Click','OpenWizard_Click','ExportSettings_Click','ImportSettings_Click','UndoGameMode_Click','UndoDisplay_Click') {
    if ($page.SelectNodes("//*[local-name()='Button' and @Click='$handler']").Count -ne 1) { throw "Missing or duplicate action: $handler." }
}
Write-Output 'PASS Restore Center navigation and actions'
