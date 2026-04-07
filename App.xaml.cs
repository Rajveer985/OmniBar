using System.Windows;
using System.Windows.Forms;

namespace WinSpotlight;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private NotifyIcon?  _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _mainWindow = new MainWindow();
        SetupTray();
    }

    private void SetupTray()
    {
        _trayIcon = new NotifyIcon
        {
            Icon    = System.Drawing.SystemIcons.Application,
            Text    = "WinSpotlight  [Alt+Space]",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open  (Alt+Space)", null, (_, _) => _mainWindow?.ShowSpotlight());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon!.Visible = false;
            Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => _mainWindow?.ShowSpotlight();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
