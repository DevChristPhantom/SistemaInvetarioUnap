using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SisLabTopo.UI.Converters;

/// <summary>Cadena vacía/nula -&gt; <see cref="Visibility.Collapsed"/>; cualquier otro valor -&gt; <see cref="Visibility.Visible"/>.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Invierte un <c>bool</c> -- útil para deshabilitar/ocultar mientras <c>IsBusy</c> está activo.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
