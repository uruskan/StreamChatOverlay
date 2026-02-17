using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using StreamChatOverlay.ViewModels;

namespace StreamChatOverlay.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();

        var vm = (OverlayViewModel)DataContext;
        vm.Messages.CollectionChanged += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (ChatScrollViewer != null)
                    ChatScrollViewer.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newWidth = Width + e.HorizontalChange;
        var newHeight = Height + e.VerticalChange;
        if (newWidth >= 200) Width = newWidth;
        if (newHeight >= 150) Height = newHeight;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = DataContext,
            Owner = this
        };
        settingsWindow.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var vm = (OverlayViewModel)DataContext;
        vm.SaveSettings();
        Application.Current.Shutdown();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
    }
}
