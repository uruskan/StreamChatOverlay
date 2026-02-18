using System.Drawing;
using System.Windows;
using H.NotifyIcon.Core;
using StreamChatOverlay.ViewModels;
using StreamChatOverlay.Views;

namespace StreamChatOverlay;

public partial class App : Application
{
    private TrayIconWithContextMenu? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new TrayIconWithContextMenu
        {
            ContextMenu = CreateContextMenu()
        };
        _trayIcon.Create();
        _trayIcon.UpdateToolTip("Stream Chat Overlay");

        using var icon = CreateAppIcon();
        _trayIcon.UpdateIcon(icon.Handle);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        using var font = new Font("Arial", 6, System.Drawing.FontStyle.Bold);
        g.Clear(Color.FromArgb(145, 70, 255)); // Twitch purple
        g.DrawString("SC", font, Brushes.White, 0, 2);
        var hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var clonedIcon = (Icon)icon.Clone();
        icon.Dispose();
        DestroyIcon(hIcon);
        return clonedIcon;
    }

    private PopupMenu CreateContextMenu()
    {
        var menu = new PopupMenu();

        menu.Items.Add(new PopupMenuItem("Show Settings", (_, _) => Dispatcher.Invoke(TrayShowSettings)));

        menu.Items.Add(new PopupMenuItem("Toggle Borders", (_, _) =>
            Dispatcher.Invoke(() => GetViewModel()?.ToggleBordersCommand.Execute(null))));

        menu.Items.Add(new PopupMenuItem("Reset Window Position", (_, _) =>
            Dispatcher.Invoke(() =>
            {
                var vm = GetViewModel();
                if (vm == null) return;
                vm.Settings.WindowLeft = 100;
                vm.Settings.WindowTop = 100;
                vm.Settings.WindowWidth = 350;
                vm.Settings.WindowHeight = 600;
            })));

        menu.Items.Add(new PopupMenuSeparator());

        menu.Items.Add(new PopupMenuItem("Clear Chat", (_, _) =>
            Dispatcher.Invoke(() => GetViewModel()?.ClearChatCommand.Execute(null))));

        menu.Items.Add(new PopupMenuSeparator());

        menu.Items.Add(new PopupMenuItem("Exit", (_, _) =>
            Dispatcher.Invoke(() =>
            {
                GetViewModel()?.SaveSettings();
                Shutdown();
            })));

        return menu;
    }

    private OverlayViewModel? GetViewModel()
        => (MainWindow as OverlayWindow)?.DataContext as OverlayViewModel;

    private void TrayShowSettings()
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
}
