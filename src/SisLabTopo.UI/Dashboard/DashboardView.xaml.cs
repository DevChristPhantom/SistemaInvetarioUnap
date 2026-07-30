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
///
/// <para>
/// <b>Bug real (QA manual) corregido aquí</b>: al alternar a modo oscuro con el Dashboard
/// abierto, el fondo (<c>Background="{DynamicResource CardBrush}"</c>) de ambos gráficos
/// se quedaba con el color del tema anterior hasta que el usuario pasaba el mouse por
/// encima -- recién ahí tomaba el color correcto. Causa: los gráficos de LiveCharts2 no
/// se pintan sobre el árbol visual normal de WPF, sino sobre un <c>CoreMotionCanvas</c>
/// (SkiaSharp) propio que solo se repinta cuando algo llama explícitamente a
/// <c>Invalidate()</c> -- WPF notifica el cambio de <c>DynamicResource</c> a la propiedad
/// <c>Background</c> con normalidad, pero esa notificación nunca llega a disparar un
/// nuevo frame de Skia. El mouse "arreglaba" el color porque el pipeline de hit-testing
/// de LiveCharts2 invalida el canvas como efecto secundario de cada movimiento. Se
/// suscribe aquí (no en el ViewModel) a <c>IThemeService.TemaCambiado</c> -- expuesto por
/// <see cref="DashboardViewModel.ThemeService"/> -- en Loaded/Unloaded de forma simétrica:
/// este ViewModel es Transient (uno nuevo por cada navegación al Dashboard) pero
/// <c>IThemeService</c> es Singleton, así que suscribirse sin desuscribirse dejaría cada
/// instancia vieja referenciada para siempre por el singleton.
/// </para>
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>Gris neutro (Tailwind gray-500): contraste aceptable tanto sobre CardBrush claro como oscuro, sin necesitar reaccionar al cambio de tema en caliente.</summary>
    private static readonly SolidColorPaint LegendTextPaintNeutro = new(new SKColor(0x6B, 0x72, 0x80));

    private DashboardViewModel? _vmSuscrito;

    public DashboardView()
    {
        InitializeComponent();

        GraficoEstadoEquipos.LegendTextPaint = LegendTextPaintNeutro;

        Loaded += async (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                _vmSuscrito = vm;
                vm.ThemeService.TemaCambiado += OnTemaCambiado;

                if (vm.CargarCommand.CanExecute(null))
                {
                    await vm.CargarCommand.ExecuteAsync(null);
                }
            }
        };

        Unloaded += (_, _) =>
        {
            if (_vmSuscrito is not null)
            {
                _vmSuscrito.ThemeService.TemaCambiado -= OnTemaCambiado;
                _vmSuscrito = null;
            }
        };
    }

    private void OnTemaCambiado(object? sender, Theming.AppTheme tema)
    {
        Dispatcher.InvokeAsync(() =>
        {
            GraficoEstadoEquipos.CoreCanvas.Invalidate();
            GraficoPrestamosPorMes.CoreCanvas.Invalidate();
        });
    }
}
