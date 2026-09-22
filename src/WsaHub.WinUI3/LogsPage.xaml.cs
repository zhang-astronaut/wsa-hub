using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class LogsPage : Page
{
    readonly TextBox _box = new()
    {
        AcceptsReturn = true,
        IsReadOnly = true,
        Height = 480,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
    };

    public LogsPage()
    {
        InitializeComponent();
        Root.Content = Ui.Page(
            Ui.Card("日志",
                Ui.Btn("安装日志", () => Load(Path.Combine(MainWindow.Config.WsaInstallDir, "_install_apply_log.txt"))),
                Ui.Btn("config.json", () => Load(HubConfig.DefaultPath())),
                Ui.Btn("register.ps1", () => Load(Path.Combine(MainWindow.Config.WsaInstallDir, "_register.ps1"))),
                _box
            )
        );
        Load(Path.Combine(MainWindow.Config.WsaInstallDir, "_install_apply_log.txt"));
    }

    void Load(string path)
    {
        try { _box.Text = File.Exists(path) ? File.ReadAllText(path) : "(空) " + path; }
        catch (Exception ex) { _box.Text = ex.Message; }
    }
}
