using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FuturesTicker.Services;

namespace FuturesTicker.Converters;

/// <summary>
/// Maps a price change value to a brush. Follows the Chinese market convention:
/// up = red, down = green.
/// Parameter "bg" returns a light tint for badge backgrounds, otherwise a solid color.
/// When <see cref="AppConfig.ShowColor"/> is false, all values map to a neutral gray.
/// </summary>
public class ChangeBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Up = Make(0xE5, 0x39, 0x35);
    private static readonly SolidColorBrush Down = Make(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush Flat = Make(0x9E, 0x9E, 0x9E);

    private static readonly SolidColorBrush UpBg = Make(0xFD, 0xEC, 0xEA);
    private static readonly SolidColorBrush DownBg = Make(0xE8, 0xF5, 0xE9);
    private static readonly SolidColorBrush FlatBg = Make(0xF5, 0xF5, 0xF5);

    private static readonly SolidColorBrush Neutral = Make(0x37, 0x37, 0x37);
    private static readonly SolidColorBrush NeutralBg = Make(0xEE, 0xEE, 0xEE);

    private static SolidColorBrush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int sign = value is double d ? Math.Sign(d) : 0;
        bool bg = string.Equals(parameter as string, "bg", StringComparison.OrdinalIgnoreCase);

        if (!AppConfig.Current.ShowColor)
            return bg ? NeutralBg : Neutral;

        return sign switch
        {
            > 0 => bg ? UpBg : Up,
            < 0 => bg ? DownBg : Down,
            _ => bg ? FlatBg : Flat,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
