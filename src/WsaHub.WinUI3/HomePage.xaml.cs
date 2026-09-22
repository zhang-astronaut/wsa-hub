using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += async (_, __) => await RefreshAsync();
    }

    async Task RefreshAsync()
    {
        var st = await Task.Run(() => WsaProbe.Detect(MainWindow.Config));
        MainWindow.Status = st;
        Root.Content = Ui.Page(
            Ui.Card("WSA 状态",
                Ui.Lbl($"已安装：{(st.Installed ? "是" : "否")}"),
                Ui.Lbl($"版本：{st.Version}"),
                Ui.Lbl($"来源：{st.Source}"),
                Ui.Lbl($"目录：{st.InstallDir}"),
                Ui.Lbl($"运行中：{(st.Running ? "是" : "否")}"),
                Ui.Lbl($"Google 服务：{(st.GApps ? "有" : "无/未知")}"),
                Ui.Lbl($"Magisk：{(st.Magisk ? "有" : "无/未知")}"),
                Ui.Btn("重新检测", async () => await RefreshAsync())
            ),
            Ui.Lbl(st.Installed
                ? "到「更新与安装」检查 WSABuilds；到「APK / ADB」侧载应用。"
                : "未检测到 WSA：请到「更新与安装」执行全新安装。")
        );
    }
}
