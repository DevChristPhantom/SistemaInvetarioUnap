using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// ViewModel del formulario de Nuevo Préstamo. Puerto funcional de
/// <c>NuevoPrestamoDialog.java</c>: replica fielmente la ficha física de solicitud de
/// equipos del laboratorio, incluyendo el <b>límite fijo de 6 ítems</b> (decisión
/// explícita del plan de migración: "Comportamiento a preservar tal cual" -- no se hace
/// dinámico) y la lista fija de semestres.
///
/// Mejora deliberada respecto a Java: validación por campo vía
/// <see cref="ObservableValidator"/>/<see cref="INotifyDataErrorInfo"/> en vez de un
/// único <c>JOptionPane</c> con "Todos los campos del solicitante son obligatorios."
/// </summary>
public partial class NuevoPrestamoViewModel : ObservableValidator
{
    private readonly IPrestamoService _prestamoService;
    private readonly IEquipoService _equipoService;
    private readonly IComprobantePrestamoGenerator _comprobanteGenerator;
    private readonly IDialogService _dialogService;
    private readonly ILogger? _logger;

    /// <summary>Igual que <c>AppConfig.DEFAULT_DOCENTE</c> en Java: valor inicial editable, no un placeholder.</summary>
    public const string DocenteInicial = "Abdul Tacma Fernández";

    public const string CursoInicial = "Topografía Minera";

    public IReadOnlyList<string> SemestresDisponibles { get; } = new[] { "2026-I", "2026-II", "2027-I", "2027-II" };

    public DateTime FechaPrestamo { get; } = DateTime.Now;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El docente responsable es obligatorio.")]
    private string _docente = DocenteInicial;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El curso es obligatorio.")]
    private string _curso = CursoInicial;

    [ObservableProperty]
    private string _semestreSeleccionado;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El nombre del estudiante es obligatorio.")]
    private string _estudiante = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El código del estudiante es obligatorio.")]
    private string _codigoEstudiante = string.Empty;

    [ObservableProperty]
    private string _observaciones = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyCanExecuteChangedFor(nameof(GuardarEImprimirCommand))]
    private bool _isBusy;

    /// <summary>Exactamente 6 filas, siempre -- el límite fijo del formulario físico original (no dinámico).</summary>
    public ObservableCollection<EquipoPrestamoItemViewModel> Filas { get; } =
        new(Enumerable.Range(1, 6).Select(i => new EquipoPrestamoItemViewModel(i)));

    /// <summary>Verdadero si <see cref="GuardarAsync"/>/<see cref="GuardarEImprimirAsync"/> completó sin errores.</summary>
    public bool GuardadoExitoso { get; private set; }

    public event EventHandler? SolicitarCierre;

    public NuevoPrestamoViewModel(
        IPrestamoService prestamoService,
        IEquipoService equipoService,
        IComprobantePrestamoGenerator comprobanteGenerator,
        IDialogService dialogService,
        ILogger? logger = null)
    {
        _prestamoService = prestamoService;
        _equipoService = equipoService;
        _comprobanteGenerator = comprobanteGenerator;
        _dialogService = dialogService;
        _logger = logger;
        _semestreSeleccionado = SemestresDisponibles[0];
    }

    [RelayCommand]
    private void Buscar(EquipoPrestamoItemViewModel? fila)
    {
        if (fila is null)
        {
            return;
        }

        var yaSeleccionados = Filas.Where(f => !ReferenceEquals(f, fila) && f.TieneEquipo)
            .Select(f => f.Codigo)
            .ToList();

        var searchViewModel = new EquipoSearchViewModel(_equipoService, yaSeleccionados);
        _dialogService.ShowDialog(searchViewModel);

        if (searchViewModel.EquipoSeleccionado is { } equipo)
        {
            fila.Asignar(equipo);
        }
    }

    [RelayCommand]
    private void Quitar(EquipoPrestamoItemViewModel? fila) => fila?.Limpiar();

    private bool PuedeGuardar() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private Task GuardarAsync() => GuardarInternoAsync(imprimir: false);

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private Task GuardarEImprimirAsync() => GuardarInternoAsync(imprimir: true);

    private async Task GuardarInternoAsync(bool imprimir)
    {
        MensajeError = string.Empty;
        ValidateAllProperties();

        var codigos = Filas.Where(f => f.TieneEquipo).Select(f => f.Codigo).ToList();
        if (codigos.Count == 0)
        {
            MensajeError = "Debe seleccionar al menos un equipo para realizar el préstamo.";
        }

        if (HasErrors || codigos.Count == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var prestamo = new Prestamo
            {
                Id = Guid.NewGuid().ToString(),
                Docente = Docente.Trim(),
                Curso = Curso.Trim(),
                Semestre = SemestreSeleccionado,
                NombreEstudiante = Estudiante.Trim(),
                CodigoEstudiante = CodigoEstudiante.Trim(),
                FechaPrestamo = DateTime.Now,
                Estado = EstadoPrestamo.Activo,
                Observaciones = Observaciones.Trim(),
                FechaRegistro = DateTime.Now
            };

            var prestamoId = await _prestamoService.RegistrarPrestamoAsync(prestamo, codigos);
            prestamo.Id = prestamoId;
            GuardadoExitoso = true;

            if (imprimir)
            {
                await MostrarComprobanteAsync(prestamo);
            }

            SolicitarCierre?.Invoke(this, EventArgs.Empty);
        }
        catch (ServiceException ex)
        {
            MensajeError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MostrarComprobanteAsync(Prestamo prestamo)
    {
        // El préstamo YA quedó registrado en este punto -- un fallo al generar/mostrar
        // el comprobante no debe hacer parecer que el préstamo no se guardó, así que se
        // informa el error por separado sin relanzarlo (equivalente a que Java, tras
        // "guardar(true)", ya había hecho commit y solo fallaría al imprimir).
        try
        {
            var detalles = await _prestamoService.ObtenerDetalleAsync(prestamo.Id);
            var equipos = await _equipoService.ObtenerTodosAsync();
            var previewViewModel = new ComprobantePreviewViewModel(_comprobanteGenerator, prestamo, detalles, equipos);
            await previewViewModel.CargarAsync();
            _dialogService.ShowDialog(previewViewModel);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "El préstamo {PrestamoId} se registró, pero falló la vista previa del comprobante.", prestamo.Id);
            _dialogService.ShowError(
                $"El préstamo se registró correctamente, pero no se pudo generar/mostrar el comprobante: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancelar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
