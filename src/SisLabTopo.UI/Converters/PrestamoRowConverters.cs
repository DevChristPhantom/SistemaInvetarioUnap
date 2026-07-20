using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SisLabTopo.UI.Converters;

/// <summary>
/// <see cref="DateTime"/> de préstamo -&gt; color de fondo de fila, idéntico al criterio
/// de <c>PrestamosPanel.java</c> (renderer de filas): blanco si &lt;=7 días de
/// antigüedad, <c>RowOverdueWarningBrush</c> si 7-14 días, <c>RowOverdueDangerBrush</c>
/// si &gt;14 días. Se implementa como <see cref="IValueConverter"/> + <c>Style</c> (en vez
/// de <c>DataTrigger</c>) para poder expresar la comparación numérica de días con una
/// sola condición en vez de tres <c>DataTrigger</c> con rangos difíciles de declarar en
/// XAML puro.
/// </summary>
public sealed class FechaPrestamoToRowBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime fechaPrestamo)
        {
            return Brushes.White;
        }

        var diasActivo = (DateTime.Now - fechaPrestamo).TotalDays;

        if (diasActivo > 14)
        {
            return BuscarRecurso("RowOverdueDangerBrush") ?? Brushes.MistyRose;
        }

        if (diasActivo > 7)
        {
            return BuscarRecurso("RowOverdueWarningBrush") ?? Brushes.LightYellow;
        }

        return Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush? BuscarRecurso(string key) =>
        Application.Current?.TryFindResource(key) as Brush;
}

/// <summary>Primeros 8 caracteres de un identificador (típicamente un GUID), igual que <c>p.getId().substring(0, 8)</c> en Java.</summary>
public sealed class IdCortoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var texto = value as string ?? string.Empty;
        return texto.Length > 8 ? texto[..8] : texto;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
