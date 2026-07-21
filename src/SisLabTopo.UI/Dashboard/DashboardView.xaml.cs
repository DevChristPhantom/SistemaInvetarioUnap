using System.Windows.Controls;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// Vista del Dashboard. Código-behind: disparar la carga inicial al aparecer en pantalla
/// (equivalente a <c>MainFrame.mostrarWorkspace()</c> llamando
/// <c>DashboardPanel.refrescarDatos()</c> en Java), mismo patrón que
/// <c>EquiposView</c>/<c>PrestamosView</c> de la Fase 5a; y fijar el color del texto de la
/// leyenda del donut (ver comentario junto a <c>GraficoEstadoEquipos</c> en el XAML: bug
/// real de modo oscuro encontrado en la verificación visual de la Fase B, donde el fondo
/// del gráfico pasó a ser oscuro pero LiveCharts2 seguía dibujando la leyenda en negro).
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>Gris neutro (Tailwind gray-500): contraste aceptable tanto sobre CardBrush claro como oscuro, sin necesitar reaccionar al cambio de tema en caliente.</summary>
    private static readonly SolidColorPaint LegendTextPaintNeutro = new(new SKColor(0x6B, 0x72, 0x80));

    public DashboardView()
    {
        InitializeComponent();

        GraficoEstadoEquipos.LegendTextPaint = LegendTextPaintNeutro;

        Loaded += async (_, _) =>
        {
            if (DataContext is DashboardViewModel vm && vm.CargarCommand.CanExecute(null))
            {
                await vm.CargarCommand.ExecuteAsync(null);
            }
        };
    }
}
