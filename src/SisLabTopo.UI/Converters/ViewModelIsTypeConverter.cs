using System.Globalization;
using System.Windows.Data;

namespace SisLabTopo.UI.Converters;

/// <summary>
/// Compara el nombre del tipo CLR de <paramref name="value"/> (típicamente
/// <c>ShellViewModel.CurrentViewModel</c>) contra el nombre pasado como
/// <c>ConverterParameter</c> (p.ej. "DashboardViewModel"). Se usa para resaltar el botón
/// activo del sidebar -- equivalente a la comparación <c>cardName.equals(currentCard)</c>
/// de <c>MainFrame.crearSidebar()</c> en Java -- sin que <see cref="Shell.ShellViewModel"/>
/// tenga que mantener un string redundante de "destino activo" sincronizado a mano.
/// </summary>
public sealed class ViewModelIsTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string expectedTypeName &&
        string.Equals(value.GetType().Name, expectedTypeName, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
