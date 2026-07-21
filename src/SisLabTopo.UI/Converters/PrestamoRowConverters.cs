using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SisLabTopo.UI.Converters;

/// <summary>
/// <see cref="DateTime"/> de préstamo -&gt; categoría de antigüedad ("Normal"/"Warning"/
/// "Danger"), idéntico al criterio de <c>PrestamosPanel.java</c> (renderer de filas):
/// Normal si &lt;=7 días, Warning si 7-14, Danger si &gt;14. Devuelve una categoría, NUNCA
/// un <see cref="Brush"/> resuelto: el color real lo pone <c>PrestamoRowStyle</c> (ver
/// <c>PrestamosView.xaml</c>) con un <c>DataTrigger</c> por categoría + <c>DynamicResource</c>.
///
/// <para>
/// <b>Por qué NO como antes</b> (bug real encontrado en la verificación visual de la Fase
/// B, en modo oscuro): la versión anterior de este converter devolvía directamente un
/// <see cref="Brush"/> ya resuelto (<c>Brushes.White</c> hardcodeado para el caso
/// "Normal", o el resultado de <c>TryFindResource</c> capturado en el momento de la
/// conversión para los otros dos casos). Un <see cref="Binding"/> solo vuelve a invocar
/// <c>Convert()</c> cuando cambia la propiedad ORIGEN (<c>Prestamo.FechaPrestamo</c>), no
/// cuando <c>ThemeService</c> intercambia <c>Application.Resources</c> en caliente -- así
/// que las filas "Normal" quedaban con fondo blanco fijo sin importar el tema (texto
/// blanco de modo oscuro sobre fondo blanco: ilegible), exactamente el mismo
/// anti-patrón que ya se documentó evitar en <c>StatCard.xaml</c>. Con este converter
/// devolviendo solo una categoría (que nunca depende del tema), y el color resuelto vía
/// <c>DynamicResource</c> en el <c>Style</c>, el caso "Normal" hereda el
/// <c>CardBrush</c> del <c>Style</c> base (ya reactivo al tema) y Warning/Danger se
/// repintan solos al alternar el tema, sin recargar la tabla.
/// </para>
/// </summary>
public sealed class FechaPrestamoToRowCategoriaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime fechaPrestamo)
        {
            return "Normal";
        }

        var diasActivo = (DateTime.Now - fechaPrestamo).TotalDays;

        if (diasActivo > 14)
        {
            return "Danger";
        }

        if (diasActivo > 7)
        {
            return "Warning";
        }

        return "Normal";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
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
