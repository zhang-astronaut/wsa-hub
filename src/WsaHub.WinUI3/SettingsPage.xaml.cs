using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        var cfg = MainWindow.Config;
        var tbInst = new TextBox { Text = cfg.WsaInstallDir, Width = 480 };
        var tbDl = new TextBox { Text = cfg.DownloadDir, Width = 480 };
        var tbPat = new TextBox { Text = cfg.AssetPattern, Width = 480 };
        var tbToken = new PasswordBox { Width = 480 };
        Root.Content = Ui.Page(
            Ui.Card("路径",
                Ui.Lbl("WSA 安装目录"), tbInst,
                Ui.Lbl("下载目录"), tbDl
            ),
            Ui.Card("更新策略",
                Ui.Lbl("资产正则"), tbPat,
                Ui.Lbl("GitHub Token（可选）"), tbToken
            ),
            Ui.Card("操作",
                Ui.Btn("保存设置", () =>
                {
                    cfg.WsaInstallDir = tbInst.Text.Trim();
                    cfg.DownloadDir = tbDl.Text.Trim();
                    cfg.AssetPattern = tbPat.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(tbToken.Password)) cfg.GithubToken = tbToken.Password.Trim();
                    cfg.Save();
                    _ = Ui.ShowAsync("WsaHub", "已保存 " + HubConfig.DefaultPath());
                }),
                Ui.Btn("打开 WSABuilds Releases", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/MustardChef/WSABuilds/releases",
                    UseShellExecute = true
                }))
            )
        );
    }
}
