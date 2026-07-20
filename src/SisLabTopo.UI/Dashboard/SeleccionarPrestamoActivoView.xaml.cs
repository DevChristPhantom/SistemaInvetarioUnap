using System.Windows;

namespace SisLabTopo.UI.Dashboard;

/// <summary>Vista del selector de préstamo activo. Único code-behind: cerrarse al pedirlo el ViewModel.</summary>
public partial class SeleccionarPrestamoActivoView : Window
{
    public SeleccionarPrestamoActivoView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SeleccionarPrestamoActivoViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is SeleccionarPrestamoActivoViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnSolicitarCierre(object? sender, EventArgs e)
    {
        DialogResult = sender is SeleccionarPrestamoActivoViewModel { PrestamoSeleccionado: not null };
    }
}
