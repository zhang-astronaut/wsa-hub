using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class AppsPage : Page
{
    readonly ListView _list = new() { Height = 360 };
    readonly TextBox _filter = new() { PlaceholderText = "过滤包名", Width = 280 };

    public AppsPage()
    {
        InitializeComponent();
        Root.Content = Ui.Page(
            Ui.Card("第三方应用",
                Ui.Btn("刷新", Reload),
                _filter,
                _list,
                Ui.Btn("启动选中", () =>
                {
                    if (_list.SelectedItem is string pkg)
                        WsaProbe.AdbShell(MainWindow.Config, $"shell monkey -p {pkg} -c android.intent.category.LAUNCHER 1");
                }),
                Ui.Btn("卸载选中", async () =>
                {
                    if (_list.SelectedItem is not string pkg) return;
                    if (!await Ui.ConfirmAsync("WsaHub", "卸载 " + pkg + "？")) return;
                    WsaProbe.AdbShell(MainWindow.Config, "shell pm uninstall " + pkg);
                    Reload();
                })
            )
        );
        Loaded += (_, __) => Reload();
    }

    void Reload()
    {
        try
        {
            AdbTool.EnsureConnected(MainWindow.Config);
            var text = WsaProbe.AdbShell(MainWindow.Config, "shell pm list packages -3");
            var f = _filter.Text?.Trim() ?? "";
            _list.ItemsSource = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Replace("package:", "").Trim())
                .Where(p => p.Length > 0 && (f.Length == 0 || p.Contains(f, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        catch (Exception ex) { _list.ItemsSource = new[] { ex.Message }; }
    }
}
