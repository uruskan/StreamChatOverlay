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

    private static void OnFragmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;
        textBlock.Inlines.Clear();

        if (e.NewValue is not IList<MessageFragment> fragments) return;

        foreach (var fragment in fragments)
        {
            if (fragment.Type == FragmentType.Text)
            {
                textBlock.Inlines.Add(new Run(fragment.Content));
            }
            else if (fragment.Type == FragmentType.Emote && fragment.EmoteUrl != null)
            {
                var image = new Image
                {
                    Height = 28,
                    Width = 28,
                    Stretch = Stretch.Uniform,
                    ToolTip = fragment.Content,
                    Margin = new Thickness(2, 0, 2, 0)
                };

                if (fragment.IsAnimated)
                {
                    var uri = new Uri(fragment.EmoteUrl);
                    ImageBehavior.SetAnimatedSource(image, new BitmapImage(uri));
                    ImageBehavior.SetRepeatBehavior(image,
                        System.Windows.Media.Animation.RepeatBehavior.Forever);
                }
                else
                {
                    image.Source = new BitmapImage(new Uri(fragment.EmoteUrl))
                    {
                        CacheOption = BitmapCacheOption.OnLoad
                    };
                }

                textBlock.Inlines.Add(new InlineUIContainer(image)
                {
                    BaselineAlignment = BaselineAlignment.Center
                });
            }
        }
    }
}
