using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// ViewModel del selector "elija un préstamo activo" que antecede a la Devolución
/// Rápida del Dashboard. Puerto funcional del <c>JDialog</c> anónimo construido a mano
/// en <c>DashboardPanel.registrarDevolucionRapida()</c> (Java): un combo con los
/// préstamos activos + Confirmar/Cancelar. Al confirmar, quien abrió este diálogo
/// (<see cref="DashboardViewModel"/>) continúa con <see cref="Prestamos.DevolucionViewModel"/>
/// para el préstamo elegido.
/// </summary>
public partial class SeleccionarPrestamoActivoViewModel : ObservableObject
{
    public IReadOnlyList<Prestamo> PrestamosActivos { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarCommand))]
    private Prestamo? _prestamoElegido;

    /// <summary>Préstamo confirmado por el usuario, o <c>null</c> si canceló. Lo consulta quien abrió el diálogo.</summary>
    public Prestamo? PrestamoSeleccionado { get; private set; }

    public event EventHandler? SolicitarCierre;

    public SeleccionarPrestamoActivoViewModel(IReadOnlyList<Prestamo> prestamosActivos)
    {
        PrestamosActivos = prestamosActivos;
        _prestamoElegido = prestamosActivos.FirstOrDefault();
    }

    private bool PuedeConfirmar() => PrestamoElegido is not null;

    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private void Confirmar()
    {
        PrestamoSeleccionado = PrestamoElegido;
        SolicitarCierre?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancelar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
