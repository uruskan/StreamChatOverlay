using System.Globalization;
using System.Windows.Data;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Converters;

public class PlatformToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ChatPlatform platform
            ? platform == ChatPlatform.Twitch ? "T" : "K"
            : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
