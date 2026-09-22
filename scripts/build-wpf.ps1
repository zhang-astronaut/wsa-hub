# Build WsaHub WPF host with Roslyn csc (no NuGet).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$csc = 'C:\Program Files\dotnet\sdk\8.0.421\Roslyn\bincore\csc.dll'
if (-not (Test-Path $csc)) {
  $csc = (Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}
$wdc = (Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.*' | Sort-Object Name -Descending | Select-Object -First 1).FullName
$nc = (Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.*' | Sort-Object Name -Descending | Select-Object -First 1).FullName
Write-Host "csc=$csc"
Write-Host "wdc=$wdc"

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-nologo')
$lines.Add('-target:winexe')
$lines.Add('-platform:anycpu')
$lines.Add('-nullable:disable')
$lines.Add('-langversion:latest')
$lines.Add('-out:"' + (Join-Path $dist 'WsaHub.exe') + '"')

$must = @(
  'WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll',
  'System.Windows.Forms.dll','System.Drawing.Common.dll','System.Drawing.dll',
  'UIAutomationProvider.dll','UIAutomationTypes.dll','System.Windows.Extensions.dll'
)
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($name in $must) {
  $p = Join-Path $wdc $name
  if (Test-Path -LiteralPath $p) {
    $lines.Add('-r:"' + $p + '"')
    [void]$seen.Add($name)
  }
}
foreach ($dir in @($wdc, $nc)) {
  foreach ($f in (Get-ChildItem -LiteralPath $dir -Filter '*.dll')) {
    if ($seen.Contains($f.Name)) { continue }
    try {
      $null = [System.Reflection.AssemblyName]::GetAssemblyName($f.FullName)
      $lines.Add('-r:"' + $f.FullName + '"')
      [void]$seen.Add($f.Name)
    } catch { }
  }
}
$lines.Add('"' + (Join-Path $src 'WsaHub.Core\WsaHubCore.cs') + '"')
$lines.Add('"' + (Join-Path $src 'WsaHub.App\Program.cs') + '"')

$rsp = Join-Path $dist 'WsaHub.rsp'
Set-Content -Path $rsp -Value $lines -Encoding UTF8
Write-Host ('rsp lines=' + $lines.Count)

Write-Host 'Compiling...'
& 'C:\Program Files\dotnet\dotnet.exe' exec $csc ('@' + $rsp)
if ($LASTEXITCODE -ne 0) { throw "csc failed $LASTEXITCODE" }

@'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "frameworks": [
      { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
      { "name": "Microsoft.WindowsDesktop.App", "version": "8.0.0" }
    ]
  }
}
'@ | Set-Content (Join-Path $dist 'WsaHub.runtimeconfig.json') -Encoding UTF8

Get-Item (Join-Path $dist 'WsaHub.exe') | Format-List FullName,Length,LastWriteTime
