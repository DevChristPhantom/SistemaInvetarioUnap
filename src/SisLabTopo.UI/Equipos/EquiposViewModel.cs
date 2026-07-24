using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Equipos;

/// <summary>
/// ViewModel del panel de Inventario de Equipos. Puerto funcional de
/// <c>EquiposPanel.java</c>: filtros por Estado/Tipo (con "Todos"/"Todas"), alta/edición
/// (<see cref="EquipoFormViewModel"/> vía <see cref="IDialogService"/>), eliminación con
/// confirmación, e Importar/Exportar Excel -- todo asíncrono con <see cref="IsBusy"/>
/// deshabilitando los controles mientras carga/importa/exporta (Java bloqueaba la UI
/// entera durante estas operaciones vía <c>JFileChooser</c> + llamada síncrona).
/// </summary>
public partial class EquiposViewModel : ObservableObject, IShellContentViewModel
{
    private readonly IEquipoService _equipoService;
    private readonly IReporteService _reporteService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<EquiposViewModel> _logger;

    private List<Equipo> _todosCache = new();

    public IReadOnlyList<FiltroOpcion<EstadoEquipo>> OpcionesEstado { get; } = ConstruirOpciones(
        "Todos los Estados", e => e.Etiqueta());

    public IReadOnlyList<FiltroOpcion<TipoEquipo>> OpcionesTipo { get; } = ConstruirOpcionesTipo();

    [ObservableProperty]
    private ObservableCollection<Equipo> _equipos = new();

    [ObservableProperty]
    private FiltroOpcion<EstadoEquipo> _estadoSeleccionado;

    [ObservableProperty]
    private FiltroOpcion<TipoEquipo> _tipoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    private Equipo? _equipoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CargarCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportarExcelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarExcelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _mensajeEstado = string.Empty;

    public EquiposViewModel(
        IEquipoService equipoService,
        IReporteService reporteService,
        IDialogService dialogService,
        ILogger<EquiposViewModel> logger)
    {
        _equipoService = equipoService;
        _reporteService = reporteService;
        _dialogService = dialogService;
        _logger = logger;

        _estadoSeleccionado = OpcionesEstado[0];
        _tipoSeleccionado = OpcionesTipo[0];
    }

    private bool PuedeCargar() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task CargarAsync()
    {
        IsBusy = true;
        try
        {
            // Reinicia los filtros a "Todos"/"Todas", igual que
            // EquiposPanel.refrescarTabla() en Java (cbEstado/cbTipo.setSelectedItem(null)).
            EstadoSeleccionado = OpcionesEstado[0];
            TipoSeleccionado = OpcionesTipo[0];
            _todosCache = await _equipoService.ObtenerTodosAsync();
            AplicarFiltros();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnEstadoSeleccionadoChanged(FiltroOpcion<EstadoEquipo> value) => AplicarFiltros();

    partial void OnTipoSeleccionadoChanged(FiltroOpcion<TipoEquipo> value) => AplicarFiltros();

    private void AplicarFiltros()
    {
        IEnumerable<Equipo> datos = _todosCache;
        if (EstadoSeleccionado.Valor is { } estado)
        {
            datos = datos.Where(e => e.Estado == estado);
        }

        if (TipoSeleccionado.Valor is { } tipo)
        {
            datos = datos.Where(e => e.Tipo == tipo);
        }

        Equipos = new ObservableCollection<Equipo>(datos);
    }

    private bool PuedeAbrirFormulario() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeAbrirFormulario))]
    private async Task NuevoAsync() => await AbrirFormularioAsync(equipoExistente: null);

    private bool PuedeEditarOEliminar() => !IsBusy && EquipoSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(PuedeEditarOEliminar))]
    private async Task EditarAsync()
    {
        if (EquipoSeleccionado is { } equipo)
        {
            await AbrirFormularioAsync(equipo);
        }
    }

    /// <summary>Punto de entrada compartido por el botón "Editar" y el doble clic sobre una fila.</summary>
    public async Task AbrirFormularioAsync(Equipo? equipoExistente)
    {
        var formViewModel = new EquipoFormViewModel(_equipoService, equipoExistente);
        _dialogService.ShowDialog(formViewModel);
        if (formViewModel.GuardadoExitoso)
        {
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarOEliminar))]
    private async Task EliminarAsync()
    {
        if (EquipoSeleccionado is not { } equipo)
        {
            return;
        }

        if (!_dialogService.ShowConfirm(
                $"¿Está seguro de que desea eliminar el equipo \"{equipo.Denominacion}\"?",
                "Confirmar eliminación"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _equipoService.EliminarAsync(equipo.Codigo);
            _dialogService.ShowInfo("Equipo eliminado correctamente.");
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
        }
        catch (ServiceException ex)
        {
            _dialogService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCargar))]
    private async Task ImportarExcelAsync()
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
            MensajeEstado = "Equipos importados correctamente.";
            _dialogService.ShowInfo(MensajeEstado);
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
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
    private async Task ExportarExcelAsync()
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
            MensajeEstado = "Reporte de inventario exportado con éxito.";
            _dialogService.ShowInfo(MensajeEstado);
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

    private static IReadOnlyList<FiltroOpcion<EstadoEquipo>> ConstruirOpciones(string etiquetaTodos, Func<EstadoEquipo, string> etiqueta)
    {
        var opciones = new List<FiltroOpcion<EstadoEquipo>> { new(etiquetaTodos, null) };
        opciones.AddRange(Enum.GetValues<EstadoEquipo>().Select(e => new FiltroOpcion<EstadoEquipo>(etiqueta(e), e)));
        return opciones;
    }

    private static IReadOnlyList<FiltroOpcion<TipoEquipo>> ConstruirOpcionesTipo()
    {
        var opciones = new List<FiltroOpcion<TipoEquipo>> { new("Todas las Categorías", null) };
        opciones.AddRange(Enum.GetValues<TipoEquipo>().Select(t => new FiltroOpcion<TipoEquipo>(t.Etiqueta(), t)));
        return opciones;
    }
}
