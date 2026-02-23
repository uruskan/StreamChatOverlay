using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamChatOverlay.Models;
using WpfAnimatedGif;

namespace StreamChatOverlay.Behaviors;

public static class InlineMessageBehavior
{
    public static readonly DependencyProperty FragmentsProperty =
        DependencyProperty.RegisterAttached(
            "Fragments",
            typeof(IList<MessageFragment>),
            typeof(InlineMessageBehavior),
            new PropertyMetadata(null, OnFragmentsChanged));

    public static void SetFragments(DependencyObject element, IList<MessageFragment>? value)
        => element.SetValue(FragmentsProperty, value);

    public static IList<MessageFragment>? GetFragments(DependencyObject element)
        => (IList<MessageFragment>?)element.GetValue(FragmentsProperty);

    public static readonly DependencyProperty EmoteSizeProperty =
        DependencyProperty.RegisterAttached(
            "EmoteSize",
            typeof(double),
            typeof(InlineMessageBehavior),
            new PropertyMetadata(28.0, OnFragmentsChanged));

    public static void SetEmoteSize(DependencyObject element, double value)
        => element.SetValue(EmoteSizeProperty, value);

    public static double GetEmoteSize(DependencyObject element)
        => (double)element.GetValue(EmoteSizeProperty);

    private static void OnFragmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;
        textBlock.Inlines.Clear();

        var fragments = GetFragments(textBlock);
        if (fragments == null) return;

        var emoteSize = GetEmoteSize(textBlock);

        foreach (var fragment in fragments)
        {
            if (fragment.Type == FragmentType.Text)
            {
                textBlock.Inlines.Add(new Run(fragment.Content));
            }
            else if (fragment.Type == FragmentType.Emote && fragment.EmoteUrl != null)
            {
                try
                {
                    var image = new Image
                    {
                        Height = emoteSize,
                        Width = emoteSize,
                        Stretch = Stretch.Uniform,
                        ToolTip = fragment.Content,
                        Margin = new Thickness(2, 0, 2, 0)
                    };

                    var uri = new Uri(fragment.EmoteUrl);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = uri;
                    bitmapImage.EndInit();

                    // Always use WpfAnimatedGif - it handles both static and animated images
                    ImageBehavior.SetAnimatedSource(image, bitmapImage);
                    ImageBehavior.SetRepeatBehavior(image,
                        System.Windows.Media.Animation.RepeatBehavior.Forever);

                    textBlock.Inlines.Add(new InlineUIContainer(image)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                }
                catch
                {
                    textBlock.Inlines.Add(new Run(fragment.Content));
                }
            }
        }
    }
}
