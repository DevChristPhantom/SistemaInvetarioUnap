using System.Windows;

namespace SisLabTopo.UI.Prestamos;

/// <summary>Vista del diálogo de devolución. Único code-behind: cerrarse al pedirlo el ViewModel.</summary>
public partial class DevolucionView : Window
{
    public DevolucionView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is DevolucionViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is DevolucionViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnSolicitarCierre(object? sender, EventArgs e)
    {
        DialogResult = sender is DevolucionViewModel { DevueltoExitoso: true };
    }
}
