using System.Windows;
using System.Windows.Controls;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// Vista del panel de Préstamos activos. Code-behind: carga inicial al aparecer y
/// asignación del <c>RowStyle</c> de color por antigüedad al <c>DataGrid</c> interno de
/// <see cref="Controls.SearchableDataGrid"/> (que no expone esa propiedad como
/// dependency property propia, así que se asigna una única vez aquí en vez de
/// modificar el control compartido).
/// </summary>
public partial class PrestamosView : UserControl
{
    public PrestamosView()
    {
        InitializeComponent();

        TablaPrestamos.InnerDataGrid.RowStyle = (Style)FindResource("PrestamoRowStyle");

        Loaded += async (_, _) =>
        {
            if (DataContext is PrestamosViewModel vm && vm.CargarCommand.CanExecute(null))
            {
                await vm.CargarCommand.ExecuteAsync(null);
            }
        };
    }
}
