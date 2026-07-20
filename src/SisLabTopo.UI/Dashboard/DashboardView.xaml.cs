using System.Windows.Controls;

namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// Vista del Dashboard. Único code-behind: disparar la carga inicial al aparecer en
/// pantalla (equivalente a <c>MainFrame.mostrarWorkspace()</c> llamando
/// <c>DashboardPanel.refrescarDatos()</c> en Java), mismo patrón que
/// <c>EquiposView</c>/<c>PrestamosView</c> de la Fase 5a.
/// </summary>
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is DashboardViewModel vm && vm.CargarCommand.CanExecute(null))
            {
                await vm.CargarCommand.ExecuteAsync(null);
            }
        };
    }
}
