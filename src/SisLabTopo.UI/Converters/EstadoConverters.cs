using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SisLabTopo.Domain.Enums;

namespace SisLabTopo.UI.Converters;

/// <summary>
/// Convierte un color hexadecimal ("#RRGGBB") a <see cref="SolidColorBrush"/>. Utilidad
/// compartida por los converters de estado de esta misma carpeta.
/// </summary>
internal static class BrushFromHex
{
    public static SolidColorBrush Convert(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// <see cref="EstadoEquipo"/> -&gt; color hexadecimal (idéntico a
/// <c>EstadoEquipoExtensions.ColorHex()</c>), para bindear el fondo de un
/// <see cref="SisLabTopo.UI.Controls.StatusBadge"/>.
/// </summary>
public sealed class EstadoEquipoToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EstadoEquipo estado ? BrushFromHex.Convert(estado.ColorHex()) : Brushes.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary><see cref="EstadoEquipo"/> -&gt; etiqueta en español (idéntico a <c>Etiqueta()</c>).</summary>
public sealed class EstadoEquipoToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EstadoEquipo estado ? estado.Etiqueta() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// <see cref="TipoEquipo"/> -&gt; etiqueta en español (idéntico a <c>Etiqueta()</c>).
/// Necesario para cualquier <c>ComboBox</c>/<c>ItemsControl</c> que liste
/// <see cref="TipoEquipo"/> directamente (p.ej. el combo "Categoría" de
/// <c>EquipoFormView</c>): <c>Etiqueta()</c> es un método de extensión, no una
/// propiedad, así que <c>DisplayMemberPath</c> no puede resolverlo por sí solo -- sin
/// este converter, WPF cae a <c>ToString()</c> y muestra el nombre crudo del enum en
/// PascalCase (p.ej. "EstacionTotal", "NivelTopografico") en vez de la etiqueta en
/// español (bug real encontrado en la verificación visual de la Fase 5).
/// </summary>
public sealed class TipoEquipoToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TipoEquipo tipo ? tipo.Etiqueta() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// <see cref="EstadoPrestamo"/> -&gt; color. A diferencia de <see cref="EstadoEquipo"/>,
/// el dominio no define colores para este enum (Java tampoco lo hacía); se fija aquí,
/// como decisión de la capa de UI, un mapeo coherente con la paleta general: Activo en
/// azul secundario, Devuelto en verde éxito, Vencido en rojo peligro.
/// </summary>
public sealed class EstadoPrestamoToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EstadoPrestamo.Activo => BrushFromHex.Convert("#2563EB"),
        EstadoPrestamo.Devuelto => BrushFromHex.Convert("#22C55E"),
        EstadoPrestamo.Vencido => BrushFromHex.Convert("#EF4444"),
        _ => Brushes.Gray
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary><see cref="EstadoPrestamo"/> -&gt; etiqueta en español.</summary>
public sealed class EstadoPrestamoToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EstadoPrestamo estado ? estado.Etiqueta() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// <c>bool</c> de disponibilidad de un equipo -&gt; color (verde/rojo). Existe
/// explícitamente para corregir la inconsistencia de la versión Java, donde
/// "Disponible" se mostraba como píldora coloreada en un lugar de la UI y como texto
/// plano "true"/"false" en otro: en la Fase 5, toda pantalla que muestre disponibilidad
/// debe bindear un <see cref="SisLabTopo.UI.Controls.StatusBadge"/> a este converter (y
/// a <see cref="DisponibilidadToTextConverter"/>) en vez de mostrar el booleano crudo.
/// </summary>
public sealed class DisponibilidadToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? BrushFromHex.Convert("#22C55E") : BrushFromHex.Convert("#EF4444");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary><c>bool</c> de disponibilidad -&gt; "Disponible" / "No disponible".</summary>
public sealed class DisponibilidadToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Disponible" : "No disponible";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
