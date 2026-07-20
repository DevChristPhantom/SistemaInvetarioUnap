using System.Windows.Controls;

namespace SisLabTopo.UI.Shell;

/// <summary>
/// Vista genérica para cualquier <see cref="PlaceholderViewModel"/> (Dashboard/Equipos/
/// Préstamos/Historial/Configuración de la Fase 4): un único DataTemplate en
/// <c>App.xaml</c> la mapea a todas las subclases de <see cref="PlaceholderViewModel"/>,
/// ya que WPF resuelve la plantilla implícita por el tipo o el tipo base más cercano.
/// </summary>
public partial class PlaceholderView : UserControl
{
    public PlaceholderView()
    {
        InitializeComponent();
    }
}
