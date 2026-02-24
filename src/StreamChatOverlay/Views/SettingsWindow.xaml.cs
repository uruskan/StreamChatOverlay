using System.Windows;
using StreamChatOverlay.ViewModels;

namespace StreamChatOverlay.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        var vm = (OverlayViewModel)DataContext;
        var soundName = vm.Settings.NotificationSound;
        var volume = vm.Settings.NotificationVolume;
        vm.PreviewSound(soundName, volume);
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        var vm = (OverlayViewModel)DataContext;
        vm.UpdateNotificationSound();
        vm.SaveSettings();
        Close();
    }
}
