using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace iTunesMatchTagger.Wpf;

/// <summary>Null -> Collapsed (or Visible when Inverted is set).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Inverted { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null;
        if (Inverted)
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
