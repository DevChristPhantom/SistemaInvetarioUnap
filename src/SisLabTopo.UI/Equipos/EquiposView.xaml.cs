using System.Windows.Controls;
using System.Windows.Input;

namespace SisLabTopo.UI.Equipos;

/// <summary>
/// Vista del panel de Equipos. Dos únicas responsabilidades de code-behind (el resto es
/// binding puro): disparar la carga inicial al aparecer en pantalla (equivalente al
/// <c>refrescarTabla()</c> que llamaba el constructor de <c>EquiposPanel.java</c>) y
/// abrir el formulario de edición al hacer doble clic sobre una fila (Java:
/// <c>table.addDobleClickListener(this::abrirFormulario)</c>).
/// </summary>
public partial class EquiposView : UserControl
{
    public EquiposView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is EquiposViewModel vm && vm.CargarCommand.CanExecute(null))
            {
                await vm.CargarCommand.ExecuteAsync(null);
            }
        };

        TablaEquipos.InnerDataGrid.MouseDoubleClick += async (_, e) => await TablaEquipos_MouseDoubleClickAsync(e);
    }

    private async Task TablaEquipos_MouseDoubleClickAsync(MouseButtonEventArgs e)
    {
        if (DataContext is not EquiposViewModel vm || vm.EquipoSeleccionado is null || vm.IsBusy)
        {
            return;
        }

        await vm.AbrirFormularioAsync(vm.EquipoSeleccionado);
    }
}
