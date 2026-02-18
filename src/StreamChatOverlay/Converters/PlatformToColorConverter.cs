using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Converters;

public class PlatformToColorConverter : IValueConverter
{
    private static readonly Brush TwitchBrush = new SolidColorBrush(Color.FromRgb(145, 70, 255)); // #9146FF
    private static readonly Brush KickBrush = new SolidColorBrush(Color.FromRgb(83, 252, 24));   // #53FC18

    static PlatformToColorConverter()
    {
        TwitchBrush.Freeze();
        KickBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChatPlatform platform)
            return platform == ChatPlatform.Twitch ? TwitchBrush : KickBrush;
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
