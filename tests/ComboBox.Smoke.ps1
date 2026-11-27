# Run with: powershell.exe -NoProfile -STA -File tests/ComboBox.Smoke.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework

function Read-Xaml($file) {
    [xml][IO.File]::ReadAllText((Join-Path $PSScriptRoot "../WinTweaker/$file"))
}

function Get-Visuals($element) {
    $element
    for ($i = 0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($element); $i++) {
        Get-Visuals ([Windows.Media.VisualTreeHelper]::GetChild($element, $i))
    }
}

function Get-BackgroundPixel($box) {
    $box.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(300, 42, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($box)
    $pixel = [byte[]]::new(4)
    $bitmap.CopyPixels([Windows.Int32Rect]::new(250, 20, 1, 1), $pixel, 4, 0)
    $pixel[0..2]
}

# Drive WPF's real theme triggers without moving the user's mouse.
$mouseOverKey = [Windows.UIElement].GetField('IsMouseOverPropertyKey', 'NonPublic,Static').GetValue($null)
$appStyles = (Read-Xaml 'App.xaml').SelectNodes('//*[local-name()="Application.Resources"]/*[local-name()="ResourceDictionary"]/*[local-name()="Style"]')
$failures = @()
$checked = 0
foreach ($page in 'TweaksPage', 'SettingsPage') {
    $doc = Read-Xaml "Pages/$page.xaml"
    $styles = $doc.SelectNodes('//*[local-name()="Page.Resources"]/*[@TargetType="ComboBox" or @TargetType="ComboBoxItem"]')
    $resources = (($appStyles + $styles) | ForEach-Object OuterXml) -join ''

    foreach ($source in $doc.SelectNodes('//*[local-name()="ComboBox"]')) {
        $name = $source.GetAttribute('Name', 'http://schemas.microsoft.com/winfx/2006/xaml')
        $path = $source.GetAttribute('DisplayMemberPath')
        $localResources = ($source.SelectNodes('./*[local-name()="ComboBox.Resources"]/*') | ForEach-Object OuterXml) -join ''
        $items = ($source.SelectNodes('./*[local-name()="ComboBoxItem"]') | ForEach-Object OuterXml) -join ''
        $box = [Windows.Markup.XamlReader]::Parse(@"
<ComboBox xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="300" Height="42" DisplayMemberPath="$path">
    <ComboBox.Resources>$resources$localResources</ComboBox.Resources>
    $items
</ComboBox>
"@)
        $box.Style = $box.Resources[[Windows.Controls.ComboBox]]

        if ($path -eq 'Name') {
            $expected = 'Ethernet'
            [void]$box.Items.Add([IO.FileInfo]::new($expected))
        } elseif ($path -eq 'DisplayName') {
            $expected = 'Example option'
            [void]$box.Items.Add([ComponentModel.DisplayNameAttribute]::new($expected))
        } else {
            $expected = $box.Items[0].Content
        }

        $box.SelectedIndex = 0
        $box.Measure([Windows.Size]::new(300, 42))
        $box.Arrange([Windows.Rect]::new(0, 0, 300, 42))
        $visuals = @(Get-Visuals $box)
        $toggle = $visuals | Where-Object { $_ -is [Windows.Controls.Primitives.ToggleButton] }
        $box.SetValue($mouseOverKey, $true)
        $toggle.SetValue($mouseOverKey, $true)
        $rgb = @(Get-BackgroundPixel $box)
        if (($rgb | Measure-Object -Maximum).Maximum -gt 96) {
            $failures += "${name}: hover background is too light ($($rgb -join ', '))."
        }
        $label = $visuals | Where-Object { $_ -is [Windows.Controls.TextBlock] -and $_.Text -eq $expected }
        if (-not $label) { $failures += "${name}: selected label '$expected' is missing." }
        if (-not ($failures -match "^${name}:")) { Write-Output "PASS $name" }
        if ($name -eq 'AccentColorComboBox') {
            $colors = foreach ($item in $box.Items) {
                $box.SelectedItem = $item
                $box.UpdateLayout()
                $swatch = Get-Visuals $box | Where-Object { $_ -is [Windows.Shapes.Ellipse] } | Select-Object -First 1
                if (!$swatch.Fill) { throw "Missing accent swatch for $($item.Tag)." }
                $swatch.Fill.ToString()
            }
            if (($colors | Sort-Object -Unique).Count -ne 5) { throw 'Expected five distinct accent colors.' }
        }
        $checked++
    }
}
if ($checked -lt 4) { throw "Expected at least four dropdowns, checked $checked." }
if ($failures.Count) { throw ($failures -join "`n") }
