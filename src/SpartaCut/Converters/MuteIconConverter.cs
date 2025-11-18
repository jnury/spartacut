using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace SpartaCut.Converters;

public class MuteIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMuted)
        {
            return isMuted ? MaterialIconKind.VolumeOff : MaterialIconKind.VolumeHigh;
        }
        return MaterialIconKind.VolumeHigh;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
