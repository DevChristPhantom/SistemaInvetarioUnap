using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// ViewModel del diálogo de registro de devolución. Puerto funcional de
/// <c>DevolucionDialog.java</c>: observaciones de retorno opcionales + confirmación.
/// </summary>
public partial class DevolucionViewModel : ObservableObject
{
    private readonly IPrestamoService _prestamoService;
    private readonly string _prestamoId;

    [ObservableProperty]
    private string _observaciones = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegistrarCommand))]
    private bool _isBusy;

    /// <summary>Verdadero si la devolución se registró con éxito; lo consulta quien abrió el diálogo para decidir si debe refrescar su tabla.</summary>
    public bool DevueltoExitoso { get; private set; }

    public event EventHandler? SolicitarCierre;

    public DevolucionViewModel(IPrestamoService prestamoService, string prestamoId)
    {
        _prestamoService = prestamoService;
        _prestamoId = prestamoId;
    }

    private bool PuedeRegistrar() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeRegistrar))]
    private async Task RegistrarAsync()
    {
        IsBusy = true;
        MensajeError = string.Empty;
        try
        {
            await _prestamoService.RegistrarDevolucionAsync(_prestamoId, Observaciones.Trim());
            DevueltoExitoso = true;
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

    [RelayCommand]
    private void Cancelar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
