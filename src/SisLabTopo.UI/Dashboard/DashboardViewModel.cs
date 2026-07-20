using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Equipos;
using SisLabTopo.UI.Prestamos;
using SisLabTopo.UI.Shell;
using SkiaSharp;

namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// ViewModel del panel de Dashboard. Puerto funcional de <c>DashboardPanel.java</c>: 4
/// tarjetas de estadística, 2 gráficos (donut de estado de equipos + barras de préstamos
/// mensuales), 2 mini-tablas (últimos préstamos activos / equipos más prestados),
/// Acciones Rápidas (abre los diálogos ya construidos en la Fase 5a vía
/// <see cref="IDialogService"/>) y un widget de "Disponibilidad Rápida" que reutiliza
/// <see cref="Controls.SearchableDataGrid"/> -- el mismo control que la pantalla de
/// Equipos -- para consistencia visual real.
///
/// Los gráficos usan LiveCharts2 (<c>LiveChartsCore.SkiaSharpView.WPF</c>): es la opción
/// más idiomática para WPF/MVVM de las tres consideradas (LiveCharts2/OxyPlot/ScottPlot)
/// porque expone sus series/ejes como propiedades de ViewModel bindeables de forma
/// nativa (<see cref="ISeries"/>[], <see cref="Axis"/>[]) sin requerir ningún objeto de
/// UI en el ViewModel (a diferencia de OxyPlot, cuyo <c>PlotModel</c> mezcla más
/// responsabilidad de dibujo) y renderiza con SkiaSharp -- compatible con el
/// <c>RenderMode.SoftwareOnly</c> forzado en <c>App.OnStartup</c> para esta máquina sin
/// GPU confiable.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IShellContentViewModel
{
    private readonly IEquipoService _equipoService;
    private readonly IPrestamoService _prestamoService;
    private readonly IReporteService _reporteService;
    private readonly IComprobantePrestamoGenerator _comprobanteGenerator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<DashboardViewModel> _logger;

    [ObservableProperty]
    private string _totalEquiposTexto = "0";

    [ObservableProperty]
    private string _equiposDisponiblesTexto = "0";

    [ObservableProperty]
    private string _prestamosActivosTexto = "0";

    [ObservableProperty]
    private string _devolucionesHoyTexto = "0";

    /// <summary>Falso cuando no hay ningún equipo registrado -- la vista muestra el placeholder "Sin datos disponibles" en vez del donut.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinDatosGraficoEstado))]
    private bool _hayDatosGraficoEstado;

    public bool SinDatosGraficoEstado => !HayDatosGraficoEstado;

    [ObservableProperty]
    private ISeries[] _seriesEstadoEquipos = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _seriesPrestamosPorMes = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxesPrestamosPorMes = Array.Empty<Axis>();

    /// <summary>Fijo: fuerza el eje Y a pasos enteros (nunca decimales/notación científica), igual que <c>NumberAxis.createIntegerTickUnits()</c> en Java.</summary>
    public Axis[] YAxesPrestamosPorMes { get; } =
    {
        new()
        {
            MinLimit = 0,
            MinStep = 1,
            ForceStepToMin = true,
            Labeler = valor => ((int)Math.Round(valor)).ToString(CultureInfo.InvariantCulture)
        }
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinPrestamosActivosRecientes))]
    private bool _hayPrestamosActivosRecientes;

    public bool SinPrestamosActivosRecientes => !HayPrestamosActivosRecientes;

    [ObservableProperty]
    private ObservableCollection<Prestamo> _ultimosPrestamosActivos = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinEquiposMasPrestados))]
    private bool _hayEquiposMasPrestados;

    public bool SinEquiposMasPrestados => !HayEquiposMasPrestados;

    [ObservableProperty]
    private ObservableCollection<EquipoMasPrestadoRow> _equiposMasPrestados = new();

    [ObservableProperty]
    private ObservableCollection<Equipo> _equiposDisponiblesRapida = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CargarCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoPrestamoCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarDevolucionRapidaCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarNuevoEquipoCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportarEquiposExcelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarInventarioExcelCommand))]
    private bool _isBusy;

    public DashboardViewModel(
        IEquipoService equipoService,
        IPrestamoService prestamoService,
        IReporteService reporteService,
        IComprobantePrestamoGenerator comprobanteGenerator,
        IDialogService dialogService,
        ILogger<DashboardViewModel> logger)
    {
        _equipoService = equipoService;
        _prestamoService = prestamoService;
        _reporteService = reporteService;
        _comprobanteGenerator = comprobanteGenerator;
        _dialogService = dialogService;
        _logger = logger;
    }

    private bool PuedeCargar() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task CargarAsync()
    {
        IsBusy = true;
        try
        {
            var todos = await _equipoService.ObtenerTodosAsync();
            var disponibles = await _equipoService.ObtenerDisponiblesAsync();
            var activos = await _prestamoService.ContarActivosAsync();
            var devueltosHoy = await _prestamoService.ContarDevueltosHoyAsync();

            TotalEquiposTexto = todos.Count.ToString(CultureInfo.InvariantCulture);
            EquiposDisponiblesTexto = disponibles.Count.ToString(CultureInfo.InvariantCulture);
            PrestamosActivosTexto = activos.ToString(CultureInfo.InvariantCulture);
            DevolucionesHoyTexto = devueltosHoy.ToString(CultureInfo.InvariantCulture);

            await CargarGraficoEstadoAsync(todos.Count);
            await CargarGraficoMensualAsync();
            await CargarTablasAsync();

            EquiposDisponiblesRapida = new ObservableCollection<Equipo>(disponibles);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CargarGraficoEstadoAsync(int totalEquipos)
    {
        HayDatosGraficoEstado = totalEquipos > 0;
        if (!HayDatosGraficoEstado)
        {
            SeriesEstadoEquipos = Array.Empty<ISeries>();
            return;
        }

        // Una porción por EstadoEquipo presente (nunca se rellenan con cero los estados
        // ausentes: IEquipoService.ContarPorEstadoAsync solo agrupa lo encontrado, igual
        // que el Map que devolvía EquipoService.contarPorEstado() en Java). El color de
        // cada porción es EXACTAMENTE el mismo hex que usa StatusBadge/
        // EstadoEquipoToBrushConverter (EstadoEquipoExtensions.ColorHex()), para que el
        // donut sea visualmente consistente con cualquier StatusBadge de la app.
        var conteos = await _equipoService.ContarPorEstadoAsync();
        SeriesEstadoEquipos = conteos
            .OrderBy(par => par.Key)
            .Select(par => (ISeries)new PieSeries<int>
            {
                Values = new[] { par.Value },
                Name = par.Key.Etiqueta(),
                Fill = new SolidColorPaint(SKColor.Parse(par.Key.ColorHex())),
            })
            .ToArray();
    }

    private async Task CargarGraficoMensualAsync()
    {
        // Ya viene con relleno de ceros para los últimos 6 meses, ordenado antiguo->reciente: no recalcular.
        var porMes = await _prestamoService.PrestamosPorMesAsync(6);

        XAxesPrestamosPorMes = new[]
        {
            new Axis
            {
                Labels = porMes.Select(FormatearMes).ToArray(),
                LabelsRotation = 45,
            }
        };

        SeriesPrestamosPorMes = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = porMes.Select(p => p.Cantidad).ToArray(),
                Name = "Préstamos",
                Fill = new SolidColorPaint(SKColor.Parse("#2e6da4")),
                MaxBarWidth = 40,
            }
        };
    }

    /// <summary>"MMM yy" en español sin el punto de abreviación de .NET (p.ej. "jul 26" en vez de "jul. 26"), equivalente a <c>DateTimeFormatter.ofPattern("MMM yy")</c> en Java.</summary>
    private static string FormatearMes(PrestamosPorMesItem item)
    {
        var fecha = new DateTime(item.Anio, item.Mes, 1);
        return fecha.ToString("MMM yy", CultureInfo.GetCultureInfo("es-ES")).Replace(".", string.Empty);
    }

    private async Task CargarTablasAsync()
    {
        var activos = await _prestamoService.ObtenerActivosAsync();
        HayPrestamosActivosRecientes = activos.Count > 0;

        // Mejora deliberada sobre Java (que tomaba los primeros 5 en el orden de
        // ObtenerActivosAsync sin criterio explícito): se ordena por fecha de préstamo
        // descendente para que "Últimos Préstamos Activos" muestre, de forma literal,
        // los más recientes primero.
        UltimosPrestamosActivos = new ObservableCollection<Prestamo>(
            activos.OrderByDescending(p => p.FechaPrestamo).Take(5));

        var masPrestados = await _prestamoService.EquiposMasPrestadosAsync(5);
        HayEquiposMasPrestados = masPrestados.Count > 0;

        var filas = new List<EquipoMasPrestadoRow>();
        foreach (var item in masPrestados)
        {
            var equipo = await _equipoService.BuscarPorCodigoAsync(item.CodigoEquipo);
            filas.Add(new EquipoMasPrestadoRow(item.CodigoEquipo, equipo?.Denominacion ?? "Desconocido", item.Cantidad));
        }

        EquiposMasPrestados = new ObservableCollection<EquipoMasPrestadoRow>(filas);
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task NuevoPrestamoAsync()
    {
        var viewModel = new NuevoPrestamoViewModel(_prestamoService, _equipoService, _comprobanteGenerator, _dialogService);
        _dialogService.ShowDialog(viewModel);
        if (viewModel.GuardadoExitoso)
        {
            await CargarAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task RegistrarDevolucionRapidaAsync()
    {
        var activos = await _prestamoService.ObtenerActivosAsync();
        if (activos.Count == 0)
        {
            _dialogService.ShowInfo("No hay préstamos activos para registrar devoluciones.");
            return;
        }

        var selectorViewModel = new SeleccionarPrestamoActivoViewModel(activos);
        _dialogService.ShowDialog(selectorViewModel);
        if (selectorViewModel.PrestamoSeleccionado is not { } prestamo)
        {
            return;
        }

        var devolucionViewModel = new DevolucionViewModel(_prestamoService, prestamo.Id);
        _dialogService.ShowDialog(devolucionViewModel);
        if (devolucionViewModel.DevueltoExitoso)
        {
            await CargarAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task RegistrarNuevoEquipoAsync()
    {
        var formViewModel = new EquipoFormViewModel(_equipoService, null);
        _dialogService.ShowDialog(formViewModel);
        if (formViewModel.GuardadoExitoso)
        {
            await CargarAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task ImportarEquiposExcelAsync()
    {
        var ruta = _dialogService.ShowOpenFileDialog(
            "Archivos Excel (*.xlsx)|*.xlsx|Todos los archivos (*.*)|*.*",
            "Importar Equipos desde Excel");
        if (ruta is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _equipoService.ImportarDesdeExcelAsync(ruta);
            _dialogService.ShowInfo("Equipos importados correctamente.");
            await CargarAsync();
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning(ex, "Fallo al importar equipos desde Excel: {Ruta}", ruta);
            _dialogService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task ExportarInventarioExcelAsync()
    {
        var destino = _dialogService.ShowSaveFileDialog(
            "Archivos Excel (*.xlsx)|*.xlsx",
            "Exportar Inventario",
            "reporte_inventario.xlsx");
        if (destino is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var generado = await _reporteService.ExportarInventarioExcelAsync();
            File.Copy(generado, destino, overwrite: true);
            _dialogService.ShowInfo("Reporte de inventario exportado con éxito.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al exportar el inventario a Excel.");
            _dialogService.ShowError($"Error al guardar el reporte: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
