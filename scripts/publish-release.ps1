# Publish portable packages + single-file attempts for GitHub Release
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$msb = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$sdkSdks = 'C:\Program Files\dotnet\sdk\8.0.421\Sdks'
$proj = Join-Path $root 'src\WsaHub.WinUI3\WsaHub.WinUI3.csproj'
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $out | Out-Null

# 1) Single-file WinForms (already one exe via build-netfx)
& (Join-Path $PSScriptRoot 'build-netfx.ps1')
Copy-Item (Join-Path $out 'WsaHub.exe') (Join-Path $out 'WsaHub-winforms.exe') -Force

# 2) WinUI3 self-contained folder (reliable portable)
$pub = Join-Path $out 'winui3-portable'
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
& $msb $proj /t:Publish /p:MSBuildSDKsPath=$sdkSdks /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:WindowsAppSDKSelfContained=true /p:PublishDir=$pub\ /v:m
Write-Output "publish_exit=$LASTEXITCODE"

# 3) Try PublishSingleFile for WinUI3 (may be large / incomplete for native deps)
$sf = Join-Path $out 'winui3-single'
if (Test-Path $sf) { Remove-Item $sf -Recurse -Force }
& $msb $proj /t:Publish /p:MSBuildSDKsPath=$sdkSdks /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:WindowsAppSDKSelfContained=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishDir=$sf\ /v:m
Write-Output "single_exit=$LASTEXITCODE"

# 4) Zip portable WinUI3 for Release asset
$zip = Join-Path $out 'WsaHub-winui3-win-x64.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $pub) {
  Compress-Archive -Path (Join-Path $pub '*') -DestinationPath $zip
  Write-Output "ZIP_OK $zip"
}
Get-ChildItem $out | Select-Object Name,Length
