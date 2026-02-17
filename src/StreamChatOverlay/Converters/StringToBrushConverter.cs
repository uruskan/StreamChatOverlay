using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StreamChatOverlay.Converters;

public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                return new BrushConverter().ConvertFromString(hex) as Brush ?? Brushes.White;
            }
            catch
            {
                return Brushes.White;
            }
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
