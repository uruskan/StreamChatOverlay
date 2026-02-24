using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
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
    private static readonly HttpClient Http = new();
    private static readonly ConcurrentDictionary<string, byte[]> ImageCache = new();

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
                // Add a placeholder that will be replaced when the image loads
                var placeholder = new Run(fragment.Content);
                textBlock.Inlines.Add(placeholder);

                var url = fragment.EmoteUrl;
                var emoteName = fragment.Content;
                var tb = textBlock;
                var ph = placeholder;

                _ = LoadEmoteAsync(tb, ph, url, emoteName, emoteSize);
            }
        }
    }

    private static async Task LoadEmoteAsync(TextBlock textBlock, Run placeholder,
        string url, string emoteName, double emoteSize)
    {
        try
        {
            byte[] imageData;
            if (ImageCache.TryGetValue(url, out var cached))
            {
                imageData = cached;
            }
            else
            {
                imageData = await Http.GetByteArrayAsync(url);
                ImageCache.TryAdd(url, imageData);
            }

            // Marshal back to UI thread
            await textBlock.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var ms = new MemoryStream(imageData);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    var image = new Image
                    {
                        Height = emoteSize,
                        Width = emoteSize,
                        Stretch = Stretch.Uniform,
                        ToolTip = emoteName,
                        Margin = new Thickness(2, 0, 2, 0)
                    };

                    // Use WpfAnimatedGif for both static and animated images
                    ImageBehavior.SetAnimatedSource(image, bitmapImage);
                    ImageBehavior.SetRepeatBehavior(image,
                        System.Windows.Media.Animation.RepeatBehavior.Forever);

                    var container = new InlineUIContainer(image)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    };

                    // Replace placeholder with actual image
                    if (textBlock.Inlines.Contains(placeholder))
                    {
                        textBlock.Inlines.InsertBefore(placeholder, container);
                        textBlock.Inlines.Remove(placeholder);
                    }
                }
                catch
                {
                    // Image decode failed — keep the text placeholder
                }
            });
        }
        catch
        {
            // Download failed — the text placeholder remains visible
        }
    }
}
