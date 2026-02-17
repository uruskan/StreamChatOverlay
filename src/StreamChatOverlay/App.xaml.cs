using System.Windows;
using H.NotifyIcon;
using StreamChatOverlay.ViewModels;
using StreamChatOverlay.Views;

namespace StreamChatOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private OverlayViewModel? GetViewModel()
        => (MainWindow as OverlayWindow)?.DataContext as OverlayViewModel;

    private void TrayShowSettings_Click(object sender, RoutedEventArgs e)
    {
        if (MainWindow is OverlayWindow overlay)
        {
            var settings = new SettingsWindow
            {
                DataContext = overlay.DataContext,
                Owner = overlay
            };
            settings.ShowDialog();
        }
    }

    private void TrayToggleBorders_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel()?.ToggleBordersCommand.Execute(null);
    }

    private void TrayResetPosition_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm == null) return;
        vm.Settings.WindowLeft = 100;
        vm.Settings.WindowTop = 100;
        vm.Settings.WindowWidth = 350;
        vm.Settings.WindowHeight = 600;
    }

    private void TrayClearChat_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel()?.ClearChatCommand.Execute(null);
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel()?.SaveSettings();
        Shutdown();
    }
}
