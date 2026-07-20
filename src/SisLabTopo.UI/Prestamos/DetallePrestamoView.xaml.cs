using System.Windows;

namespace SisLabTopo.UI.Prestamos;

/// <summary>Vista de solo lectura del detalle de un préstamo. Único code-behind: cerrarse al pedirlo el ViewModel.</summary>
public partial class DetallePrestamoView : Window
{
    public DetallePrestamoView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is DetallePrestamoViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is DetallePrestamoViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnSolicitarCierre(object? sender, EventArgs e) => DialogResult = true;
}
