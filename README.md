# WsaHub
# .NET restore 本机故障：dotnet restore / nuget locals 报 path1 空引用（NuGet.Common GetFolderPath MachineWide*）。见 scripts/fix-nuget-notes.md
# 生产构建：scripts/build-wpf.ps1（csc + WindowsDesktop，零 NuGet）

## Features
- Detect WSA (install wizard if missing)
- Check WSABuilds LTS updates (GApps-NoAmazon x64)
- Download + confirm install (UAC)
- ADB APK install / packages / shell / screenshot / logcat
- Backup, logon task, settings, logs
- WinUI3 sources + MSIX packaging scripts

## Build (recommended — WinForms / .NET Framework 4.8, works today)
```powershell
cd scripts
.\build-netfx.ps1
..\dist\WsaHub.exe
```
Roslyn + FX 4.8 引用，经 mscoree 启动，**无需 NuGet**。已验证窗口标题「WsaHub — WSA 管理中心」。

```powershell
.\build-msix.ps1   # 使用已安装的 Windows SDK makeappx（无需再装 SDK）
```
产物：`dist\WsaHub.exe` + `dist\WsaHub.msix`（未签名，侧载需信任证书）。
