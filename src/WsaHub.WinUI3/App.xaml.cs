using Microsoft.UI.Xaml;

namespace WsaHub.WinUI3;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App() { InitializeComponent(); }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
