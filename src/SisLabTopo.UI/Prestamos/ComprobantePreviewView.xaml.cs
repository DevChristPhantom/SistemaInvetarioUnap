using System.Windows;

namespace SisLabTopo.UI.Prestamos;

/// <summary>Vista de vista previa del comprobante. Code-behind: cierre al pedirlo el ViewModel.</summary>
public partial class ComprobantePreviewView : Window
{
    public ComprobantePreviewView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ComprobantePreviewViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is ComprobantePreviewViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnSolicitarCierre(object? sender, EventArgs e) => DialogResult = true;
}
