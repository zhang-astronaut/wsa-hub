# NuGet restore 本机故障

`dotnet restore` / `dotnet nuget locals all --list` → `Value cannot be null. (Parameter 'path1')`

栈：`NuGetEnvironment.GetFolderPath` → `XPlatMachineWideSetting..ctor` → `GetRestoreSettingsTask`

枚举：`MachineWideSettingsBaseDirectory` / `MachineWideConfigDirectory` 失败；UserSettings 等正常。SpecialFolder 均有路径。

## 应对
1. `scripts/build-wpf.ps1` — Roslyn csc + WindowsDesktop 共享框架，零 NuGet
2. `src/WsaHub.WinUI3` — NuGet 正常后 `build-winui3.ps1`
3. MSIX 用 Windows SDK makeappx，不依赖 NuGet
