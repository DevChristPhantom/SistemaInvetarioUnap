using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Equipos;
using SisLabTopo.UI.Prestamos;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Historial;

/// <summary>
/// ViewModel del panel de Historial de Préstamos. Puerto funcional de
/// <c>HistorialPanel.java</c>: filtros por rango de fechas/Estado/Semestre + exportar a
/// Excel, evitando el mismo problema N+1 que corrigió <c>PrestamosViewModel</c> en la
/// Fase 5a (Java llamaba <c>service.obtenerDetalle(p.getId()).size()</c> desde el mapper
/// de columnas de <c>SearchableTable</c>, una vez por fila en CADA repintado).
///
/// Mejora deliberada sobre Java: los campos "Desde"/"Hasta" usan un <c>DatePicker</c> real
/// de WPF (<see cref="FechaDesde"/>/<see cref="FechaHasta"/> son <see cref="DateTime"/>?)
/// en vez de los <c>JTextField</c> de texto libre sin validación ("dd/MM/yyyy" parseado a
/// mano) que tenía <c>HistorialPanel.aplicarFiltros()</c> -- ya no puede introducirse una
/// fecha con formato inválido.
/// </summary>
public partial class HistorialViewModel : ObservableObject, IShellContentViewModel
{
    private readonly IPrestamoService _prestamoService;
    private readonly IReporteService _reporteService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<HistorialViewModel> _logger;

    private List<PrestamoRowViewModel> _cache = new();

    public IReadOnlyList<FiltroOpcion<EstadoPrestamo>> OpcionesEstado { get; } = ConstruirOpcionesEstado();

    public IReadOnlyList<string> OpcionesSemestre { get; } = new[] { "Todos", "2026-I", "2026-II", "2027-I", "2027-II" };

    [ObservableProperty]
    private DateTime? _fechaDesde = DateTime.Today.AddMonths(-6);

    [ObservableProperty]
    private DateTime? _fechaHasta = DateTime.Today;

    [ObservableProperty]
    private FiltroOpcion<EstadoPrestamo> _estadoSeleccionado;

    [ObservableProperty]
    private string _semestreSeleccionado;

    [ObservableProperty]
    private ObservableCollection<PrestamoRowViewModel> _prestamos = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CargarCommand))]
    [NotifyCanExecuteChangedFor(nameof(FiltrarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarExcelCommand))]
    private bool _isBusy;

    public HistorialViewModel(
        IPrestamoService prestamoService,
        IReporteService reporteService,
        IDialogService dialogService,
        ILogger<HistorialViewModel> logger)
    {
        _prestamoService = prestamoService;
        _reporteService = reporteService;
        _dialogService = dialogService;
        _logger = logger;

        _estadoSeleccionado = OpcionesEstado[0];
        _semestreSeleccionado = OpcionesSemestre[0];
    }

    private bool PuedeCargar() => !IsBusy;

    /// <summary>Carga inicial: reinicia los filtros a sus valores por defecto (últimos 6 meses, Todos/Todos), igual que <c>HistorialPanel.refrescarTabla()</c>.</summary>
    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task CargarAsync()
    {
        FechaDesde = DateTime.Today.AddMonths(-6);
        FechaHasta = DateTime.Today;
        EstadoSeleccionado = OpcionesEstado[0];
        SemestreSeleccionado = OpcionesSemestre[0];
        await RecargarYFiltrarAsync();
    }

    /// <summary>Botón "Filtrar": conserva los filtros actuales del usuario y vuelve a traer + aplicar sobre datos frescos.</summary>
    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task FiltrarAsync() => await RecargarYFiltrarAsync();

    private async Task RecargarYFiltrarAsync()
    {
        IsBusy = true;
        try
        {
            var todos = await _prestamoService.ObtenerTodosAsync();

            // Mismo criterio anti-N+1 que PrestamosViewModel.CargarAsync (Fase 5a): los
            // conteos de equipos por préstamo se calculan UNA SOLA VEZ por recarga, en
            // paralelo, nunca desde el render de una celda.
            var conteos = (await Task.WhenAll(todos.Select(async p =>
                    (p.Id, Cantidad: (await _prestamoService.ObtenerDetalleAsync(p.Id)).Count))))
                .ToDictionary(x => x.Id, x => x.Cantidad);

            _cache = todos.Select(p => new PrestamoRowViewModel(p, conteos.GetValueOrDefault(p.Id, 0))).ToList();
            AplicarFiltrosLocal();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AplicarFiltrosLocal()
    {
        IEnumerable<PrestamoRowViewModel> datos = _cache;

        if (FechaDesde is { } desde)
        {
            datos = datos.Where(r => r.Prestamo.FechaPrestamo.Date >= desde.Date);
        }

        if (FechaHasta is { } hasta)
        {
            datos = datos.Where(r => r.Prestamo.FechaPrestamo.Date <= hasta.Date);
        }

        if (EstadoSeleccionado.Valor is { } estado)
        {
            datos = datos.Where(r => r.Prestamo.Estado == estado);
        }

        if (!string.Equals(SemestreSeleccionado, "Todos", StringComparison.OrdinalIgnoreCase))
        {
            datos = datos.Where(r => string.Equals(r.Prestamo.Semestre, SemestreSeleccionado, StringComparison.OrdinalIgnoreCase));
        }

        Prestamos = new ObservableCollection<PrestamoRowViewModel>(datos);
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task ExportarExcelAsync()
    {
        if (FechaDesde is null || FechaHasta is null)
        {
            _dialogService.ShowError("Configure correctamente el rango de fechas para exportar.");
            return;
        }

        var destino = _dialogService.ShowSaveFileDialog(
            "Archivos Excel (*.xlsx)|*.xlsx",
            "Exportar Historial",
            "reporte_historial_prestamos.xlsx");
        if (destino is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var desde = DateOnly.FromDateTime(FechaDesde.Value);
            var hasta = DateOnly.FromDateTime(FechaHasta.Value);
            var generado = await _reporteService.ExportarHistorialExcelAsync(desde, hasta);
            File.Copy(generado, destino, overwrite: true);
            _dialogService.ShowInfo("Historial de préstamos exportado con éxito.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al exportar el historial de préstamos a Excel.");
            _dialogService.ShowError($"Error al guardar el historial: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IReadOnlyList<FiltroOpcion<EstadoPrestamo>> ConstruirOpcionesEstado()
    {
        var opciones = new List<FiltroOpcion<EstadoPrestamo>> { new("Todos", null) };
        opciones.AddRange(Enum.GetValues<EstadoPrestamo>().Select(e => new FiltroOpcion<EstadoPrestamo>(e.Etiqueta(), e)));
        return opciones;
    }
}
