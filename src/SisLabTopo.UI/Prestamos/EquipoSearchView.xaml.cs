using System.Windows;
using System.Windows.Input;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// Vista del buscador modal de equipo disponible. Code-behind: cierre al disparar
/// <c>SolicitarCierre</c>, carga inicial al aparecer, y doble clic sobre una fila como
/// atajo para seleccionar (igual que <c>SearchableTable.addDobleClickListener</c> en Java).
/// </summary>
public partial class EquipoSearchView : Window
{
    public EquipoSearchView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is EquipoSearchViewModel vm)
            {
                await vm.CargarCommand.ExecuteAsync(null);
            }
        };

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is EquipoSearchViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is EquipoSearchViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };

        TablaBusqueda.InnerDataGrid.MouseDoubleClick += TablaBusqueda_MouseDoubleClick;
    }

    private void TablaBusqueda_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is EquipoSearchViewModel vm && vm.EquipoResaltado is Equipo equipo)
        {
            vm.SeleccionarPorDobleClic(equipo);
        }
    }

    private void OnSolicitarCierre(object? sender, EventArgs e)
    {
        DialogResult = sender is EquipoSearchViewModel { EquipoSeleccionado: not null };
    }
}
