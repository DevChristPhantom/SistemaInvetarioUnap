using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// ViewModel del diálogo modal de búsqueda de equipo disponible, usado por cada fila del
/// formulario de Nuevo Préstamo. Puerto funcional de la clase interna
/// <c>NuevoPrestamoDialog.EquipoSearchDialog</c> (Java): lista solo equipos disponibles,
/// excluye <see cref="EstadoEquipo.Chatarra"/> y excluye los códigos ya elegidos en otras
/// filas del formulario que lo abrió.
/// </summary>
public partial class EquipoSearchViewModel : ObservableObject
{
    private readonly IEquipoService _equipoService;
    private readonly IReadOnlyCollection<string> _codigosYaSeleccionados;

    [ObservableProperty]
    private ObservableCollection<Equipo> _equiposDisponibles = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SeleccionarCommand))]
    private Equipo? _equipoResaltado;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Equipo elegido al confirmar (botón "Seleccionar" o doble clic); <c>null</c> si se canceló.</summary>
    public Equipo? EquipoSeleccionado { get; private set; }

    public event EventHandler? SolicitarCierre;

    public EquipoSearchViewModel(IEquipoService equipoService, IReadOnlyCollection<string> codigosYaSeleccionados)
    {
        _equipoService = equipoService;
        _codigosYaSeleccionados = codigosYaSeleccionados;
    }

    [RelayCommand]
    private async Task CargarAsync()
    {
        IsBusy = true;
        try
        {
            var disponibles = await _equipoService.ObtenerDisponiblesAsync();
            EquiposDisponibles = new ObservableCollection<Equipo>(
                disponibles
                    .Where(eq => eq.Estado != EstadoEquipo.Chatarra)
                    .Where(eq => !_codigosYaSeleccionados.Contains(eq.Codigo)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeSeleccionar() => EquipoResaltado is not null;

    [RelayCommand(CanExecute = nameof(PuedeSeleccionar))]
    private void Seleccionar()
    {
        EquipoSeleccionado = EquipoResaltado;
        SolicitarCierre?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selección directa por doble clic sobre una fila (equivalente al <c>addDobleClickListener</c> de Java).</summary>
    public void SeleccionarPorDobleClic(Equipo equipo)
    {
        EquipoSeleccionado = equipo;
        SolicitarCierre?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancelar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
