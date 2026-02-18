using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Converters;

public class PlatformToIconConverter : IValueConverter
{
    // Twitch Glitch logo (Simple Icons, 24x24 viewBox)
    private static readonly Geometry TwitchGeometry = Geometry.Parse(
        "M11.571 4.714h1.715v5.143H11.57zm4.715 0H18v5.143h-1.714zM6 0L1.714 4.286v15.428h5.143V24l4.286-4.286h3.428L22.286 12V0zm14.571 11.143l-3.428 3.428h-3.429l-3 3v-3H6.857V1.714h13.714Z");

    // Kick logo (Simple Icons, 24x24 viewBox)
    private static readonly Geometry KickGeometry = Geometry.Parse(
        "M1.333 0h8v5.333H12V2.667h2.667V0h8v8H20v2.667h-2.667v2.666H20V16h2.667v8h-8v-2.667H12v-2.666H9.333V24h-8Z");

    static PlatformToIconConverter()
    {
        TwitchGeometry.Freeze();
        KickGeometry.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChatPlatform platform)
            return platform == ChatPlatform.Twitch ? TwitchGeometry : KickGeometry;
        return Geometry.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
