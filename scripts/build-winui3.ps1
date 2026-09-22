# Build WinUI3 via Visual Studio MSBuild (works around broken `dotnet restore`).
# Requires VS MSBuild + .NET SDK Sdks path + WindowsAppSDK from NuGet.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\WsaHub.WinUI3\WsaHub.WinUI3.csproj'
$msb = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msb)) {
  $msb = (Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\MSBuild.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}
$sdkSdks = 'C:\Program Files\dotnet\sdk\8.0.421\Sdks'
if (-not (Test-Path $sdkSdks)) {
  $sdkSdks = (Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Sdks' | Sort-Object Name -Descending | Select-Object -First 1).FullName
}
Write-Host "MSBuild=$msb"
Write-Host "Sdks=$sdkSdks"
& $msb $proj /t:Restore /p:MSBuildSDKsPath=$sdkSdks /p:Platform=x64 /v:m
if ($LASTEXITCODE -ne 0) { throw "restore failed $LASTEXITCODE" }
& $msb $proj /t:Build /p:MSBuildSDKsPath=$sdkSdks /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifiers=win-x64 /v:m
if ($LASTEXITCODE -ne 0) { throw "build failed $LASTEXITCODE" }
Get-ChildItem (Join-Path $root 'src\WsaHub.WinUI3\bin') -Recurse -Filter 'WsaHub.WinUI3.exe' -EA SilentlyContinue |
  Select-Object FullName,Length
Write-Host 'Run the exe from the win-x64 output folder (self-contained App SDK payload).'
