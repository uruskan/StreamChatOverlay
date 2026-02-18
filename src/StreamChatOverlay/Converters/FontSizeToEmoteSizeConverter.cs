using System.Globalization;
using System.Windows.Data;

namespace StreamChatOverlay.Converters;

public class FontSizeToEmoteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double fontSize)
            return fontSize * 1.8;
        return 28.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
