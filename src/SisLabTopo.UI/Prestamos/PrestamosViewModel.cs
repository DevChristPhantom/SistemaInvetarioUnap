using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// ViewModel del panel "Monitoreo de Préstamos Activos". Puerto funcional de
/// <c>PrestamosPanel.java</c>: Nuevo Préstamo / Ver Detalle / Registrar Devolución /
/// Imprimir, con filas coloreadas por antigüedad. Corrige el problema N+1 de Java (ver
/// comentario detallado en <see cref="CargarAsync"/>).
/// </summary>
public partial class PrestamosViewModel : ObservableObject, IShellContentViewModel
{
    private readonly IPrestamoService _prestamoService;
    private readonly IEquipoService _equipoService;
    private readonly IComprobantePrestamoGenerator _comprobanteGenerator;
    private readonly IDialogService _dialogService;
    private readonly ILogger<PrestamosViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<PrestamoRowViewModel> _prestamos = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerDetalleCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarDevolucionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    private PrestamoRowViewModel? _prestamoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CargarCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoPrestamoCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerDetalleCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarDevolucionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    private bool _isBusy;

    public PrestamosViewModel(
        IPrestamoService prestamoService,
        IEquipoService equipoService,
        IComprobantePrestamoGenerator comprobanteGenerator,
        IDialogService dialogService,
        ILogger<PrestamosViewModel> logger)
    {
        _prestamoService = prestamoService;
        _equipoService = equipoService;
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
            var activos = await _prestamoService.ObtenerActivosAsync();

            // Carga los conteos de equipos por préstamo UNA SOLA VEZ por recarga, en
            // paralelo -- nunca desde el render de una celda/fila como hacía
            // PrestamosPanel.java (su mapper de columnas llamaba
            // service.obtenerDetalle(p.getId()).size() por CADA fila, en CADA repintado
            // de la tabla, lo que multiplicaba las consultas de forma no acotada
            // durante scroll/resize). IPrestamoService no expone un método de conteo
            // masivo (el único bulk existente, ObtenerTodosDetallesAsync, vive en
            // IPrestamoRepository/PrestamoService, fuera del alcance permitido de
            // modificar en esta fase) -- la mitigación posible sin tocar
            // SisLabTopo.Services es esta: una consulta por préstamo activo, pero
            // ejecutada exactamente una vez por recarga explícita y en paralelo.
            var conteos = (await Task.WhenAll(activos.Select(async p =>
                    (p.Id, Cantidad: (await _prestamoService.ObtenerDetalleAsync(p.Id)).Count))))
                .ToDictionary(x => x.Id, x => x.Cantidad);

            Prestamos = new ObservableCollection<PrestamoRowViewModel>(
                activos.Select(p => new PrestamoRowViewModel(p, conteos.GetValueOrDefault(p.Id, 0))));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeAbrirNuevoPrestamo() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeAbrirNuevoPrestamo))]
    private async Task NuevoPrestamoAsync()
    {
        var viewModel = new NuevoPrestamoViewModel(_prestamoService, _equipoService, _comprobanteGenerator, _dialogService);
        _dialogService.ShowDialog(viewModel);
        if (viewModel.GuardadoExitoso)
        {
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
        }
    }

    private bool HaySeleccion() => !IsBusy && PrestamoSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task VerDetalleAsync()
    {
        if (PrestamoSeleccionado is not { } fila)
        {
            return;
        }

        var detalles = await _prestamoService.ObtenerDetalleAsync(fila.Prestamo.Id);

        // Resolver la denominación de cada ítem: son a lo sumo unos pocos equipos por
        // préstamo (máximo 6, ver límite fijo del formulario de alta), así que esto es
        // una consulta puntual disparada por un clic explícito del usuario -- no el
        // patrón N+1 por repintado que sí afecta a la tabla principal.
        var filas = new List<DetalleEquipoRow>();
        var numero = 1;
        foreach (var detalle in detalles)
        {
            var equipo = await _equipoService.BuscarPorCodigoAsync(detalle.EquipoCodigo);
            filas.Add(new DetalleEquipoRow(numero++, detalle.EquipoCodigo, equipo?.Denominacion ?? "Equipo"));
        }

        var detalleViewModel = new DetallePrestamoViewModel(fila.Prestamo, filas);
        _dialogService.ShowDialog(detalleViewModel);
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task RegistrarDevolucionAsync()
    {
        if (PrestamoSeleccionado is not { } fila)
        {
            return;
        }

        var viewModel = new DevolucionViewModel(_prestamoService, fila.Prestamo.Id);
        _dialogService.ShowDialog(viewModel);
        if (viewModel.DevueltoExitoso)
        {
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
        }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task ImprimirAsync()
    {
        if (PrestamoSeleccionado is not { } fila)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var detalles = await _prestamoService.ObtenerDetalleAsync(fila.Prestamo.Id);
            var equipos = await _equipoService.ObtenerTodosAsync();
            var previewViewModel = new ComprobantePreviewViewModel(_comprobanteGenerator, fila.Prestamo, detalles, equipos, _logger);
            await previewViewModel.CargarAsync();
            _dialogService.ShowDialog(previewViewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al preparar la impresión del comprobante para el préstamo {PrestamoId}.", fila.Prestamo.Id);
            _dialogService.ShowError($"No se pudo generar el comprobante: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
