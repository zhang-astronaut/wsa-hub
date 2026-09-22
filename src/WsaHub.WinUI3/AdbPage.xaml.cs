using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class AdbPage : Page
{
    readonly TextBox _out = new() { Height = 200, IsReadOnly = true, AcceptsReturn = true, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    readonly TextBox _apk = new() { PlaceholderText = "APK 路径", Width = 420 };
    readonly TextBox _pkg = new() { PlaceholderText = "包名", Text = "com.android.vending", Width = 280 };
    readonly TextBox _shell = new() { PlaceholderText = "shell 命令（不含 shell 前缀）", Width = 420 };

    public AdbPage()
    {
        InitializeComponent();
        Root.Content = Ui.Page(
            Ui.Card("ADB 连接",
                Ui.Lbl("adb: " + (AdbTool.ResolveAdb(MainWindow.Config) ?? "未找到")),
                Ui.Btn("连接 58526", () =>
                {
                    try { AdbTool.EnsureConnected(MainWindow.Config); Append(AdbTool.Run(AdbTool.ResolveAdb(MainWindow.Config)!, "devices")); }
                    catch (Exception ex) { Append(ex.Message); }
                }),
                Ui.Btn("设备列表", () =>
                {
                    var a = AdbTool.ResolveAdb(MainWindow.Config);
                    Append(a == null ? "adb missing" : AdbTool.Run(a, "devices -l"));
                })
            ),
            Ui.Card("安装 APK",
                _apk,
                Ui.Btn("选择 APK…", async () => await PickApkAsync()),
                Ui.Btn("安装", async () =>
                {
                    if (string.IsNullOrWhiteSpace(_apk.Text) || !File.Exists(_apk.Text)) { Append("请选择 APK"); return; }
                    Append(await Task.Run(() => AdbTool.InstallApk(MainWindow.Config, _apk.Text)));
                })
            ),
            Ui.Card("包操作 / Shell",
                _pkg,
                Ui.Btn("启动包", () => Append(WsaProbe.AdbShell(MainWindow.Config, $"shell monkey -p {_pkg.Text.Trim()} -c android.intent.category.LAUNCHER 1"))),
                Ui.Btn("卸载包", async () =>
                {
                    if (!await Ui.ConfirmAsync("WsaHub", "卸载 " + _pkg.Text + "？")) return;
                    Append(WsaProbe.AdbShell(MainWindow.Config, "shell pm uninstall " + _pkg.Text.Trim()));
                }),
                Ui.Btn("截图", () =>
                {
                    var a = AdbTool.ResolveAdb(MainWindow.Config)!;
                    AdbTool.Run(a, "-s 127.0.0.1:58526 shell screencap -p /sdcard/wsahub.png");
                    Directory.CreateDirectory(MainWindow.Config.DownloadDir);
                    var dest = Path.Combine(MainWindow.Config.DownloadDir, $"wsahub_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    Append(AdbTool.Run(a, $"-s 127.0.0.1:58526 pull /sdcard/wsahub.png \"{dest}\""));
                }),
                _shell,
                Ui.Btn("执行 shell", () => Append(WsaProbe.AdbShell(MainWindow.Config, "shell " + _shell.Text.Trim()))),
                Ui.Btn("logcat -d", () => Append(WsaProbe.AdbShell(MainWindow.Config, "logcat -d -t 60"))),
                _out
            )
        );
    }

    void Append(string s) => _out.Text += s + "\n";

    async Task PickApkAsync()
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".apk");
        picker.FileTypeFilter.Add(".xapk");
        picker.FileTypeFilter.Add(".apks");
        var f = await picker.PickSingleFileAsync();
        if (f != null) _apk.Text = f.Path;
    }
}
