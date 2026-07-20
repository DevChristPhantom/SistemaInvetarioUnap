using System.Windows;

namespace SisLabTopo.UI.Prestamos;

/// <summary>Vista del formulario de Nuevo Préstamo. Único code-behind: cerrarse al pedirlo el ViewModel.</summary>
public partial class NuevoPrestamoView : Window
{
    public NuevoPrestamoView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is NuevoPrestamoViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is NuevoPrestamoViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnSolicitarCierre(object? sender, EventArgs e)
    {
        DialogResult = sender is NuevoPrestamoViewModel { GuardadoExitoso: true };
    }
}
