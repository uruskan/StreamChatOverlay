using System.Windows;
using StreamChatOverlay.ViewModels;

namespace StreamChatOverlay.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        var vm = (OverlayViewModel)DataContext;
        vm.UpdateNotificationSound();
        vm.SaveSettings();
        Close();
    }
}
