using System.Windows.Controls;

namespace SisLabTopo.UI.Historial;

/// <summary>Vista del panel de Historial. Único code-behind: disparar la carga inicial al aparecer en pantalla (mismo patrón que EquiposView/PrestamosView de la Fase 5a).</summary>
public partial class HistorialView : UserControl
{
    public HistorialView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is HistorialViewModel vm && vm.CargarCommand.CanExecute(null))
            {
                await vm.CargarCommand.ExecuteAsync(null);
            }
        };
    }
}
