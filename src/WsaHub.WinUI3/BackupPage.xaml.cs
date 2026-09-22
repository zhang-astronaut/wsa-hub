using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class BackupPage : Page
{
    public BackupPage()
    {
        InitializeComponent();
        Root.Content = Ui.Page(
            Ui.Card("userdata 备份",
                Ui.Btn("立即备份", async () =>
                {
                    var f = await Task.Run(() => WsaInstaller.BackupUserdata(MainWindow.Config));
                    await Ui.ShowAsync("WsaHub", string.IsNullOrEmpty(f) ? "未找到 userdata" : "已备份: " + f);
                }),
                Ui.Btn("打开安装目录", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = MainWindow.Config.WsaInstallDir,
                    UseShellExecute = true
                })),
                Ui.Btn("打开备份目录", () =>
                {
                    var d = Path.Combine(MainWindow.Config.WsaInstallDir, "_backup");
                    Directory.CreateDirectory(d);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = d, UseShellExecute = true });
                })
            )
        );
    }
}
