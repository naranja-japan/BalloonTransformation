using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Naranja.BalloonTransformation;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SetCurrentProcessExplicitAppUserModelID("Naranja.BalloonTransformation");

        _window = new MainWindow();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "favicon.ico");
        if (File.Exists(iconPath))
        {
            _window.AppWindow.SetIcon(iconPath);
        }

        _window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 900));
        _window.Activate();
    }
}
