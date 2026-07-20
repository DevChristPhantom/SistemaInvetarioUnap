using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;

namespace SisLabTopo.UI.PasswordRecovery;

/// <summary>
/// ViewModel del diálogo de recuperación de contraseña (Fase 6). Reemplaza el
/// <c>JOptionPane</c> de la versión Java que, ante una contraseña olvidada, indicaba
/// literalmente "edite la celda de Excel a mano o borre la base de datos completa" --
/// aquí el administrador demuestra posesión del código de recuperación generado en el
/// asistente de primer arranque (o en un restablecimiento anterior) para definir una
/// contraseña nueva, sin tocar ni destruir ningún otro dato (equipos, préstamos,
/// historial permanecen intactos).
///
/// Se abre desde <see cref="Login.LoginViewModel"/> vía <see cref="IDialogService"/>,
/// construido directamente con <c>new</c> (mismo patrón documentado en
/// <see cref="IDialogService"/> para todos los diálogos de la Fase 5): así queda
/// 100% testeable con Moq sin infraestructura de DI ni de WPF.
///
/// Dos pasos en la misma ventana (mismo patrón que
/// <c>Startup.FirstRunSetupViewModel</c>):
/// <list type="number">
/// <item><description>
/// <see cref="MostrandoCodigoNuevo"/> == <c>false</c>: código de recuperación vigente +
/// nueva contraseña (con confirmación). Sin límite de intentos fallidos para este flujo
/// -- decisión deliberada (ver comentario de <see cref="RestablecerAsync"/>): el código
/// de recuperación ya es, por diseño, un secreto de alta entropía (16 caracteres
/// Base32 ≈ 80 bits) inviable de adivinar por fuerza bruta en un número razonable de
/// intentos, a diferencia de una contraseña corta memorizable: añadir aquí el mismo
/// bloqueo de 3 intentos que tiene el login normal solo estorbaría a un administrador
/// legítimo que se equivoca transcribiendo el código, sin aportar protección real
/// adicional.
/// </description></item>
/// <item><description>
/// <see cref="MostrandoCodigoNuevo"/> == <c>true</c>: la recuperación fue exitosa; se
/// generó un código de recuperación NUEVO (el usado queda invalidado) y se muestra una
/// única vez, igual que en el asistente de primer arranque.
/// </description></item>
/// </list>
/// </summary>
public partial class PasswordRecoveryViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestablecerCommand))]
    private string _codigoRecuperacion = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeErrorNueva))]
    [NotifyPropertyChangedFor(nameof(MensajeErrorConfirmar))]
    [NotifyCanExecuteChangedFor(nameof(RestablecerCommand))]
    private string _nuevaContrasena = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeErrorConfirmar))]
    [NotifyCanExecuteChangedFor(nameof(RestablecerCommand))]
    private string _confirmarContrasena = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestablecerCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CerrarCommand))]
    private bool _mostrandoCodigoNuevo;

    [ObservableProperty]
    private string _codigoNuevo = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CerrarCommand))]
    private bool _confirmoQueGuardeElCodigo;

    /// <summary>Verdadero si la contraseña se restableció con éxito en algún momento de la vida de este diálogo.</summary>
    public bool RestablecidoExitoso { get; private set; }

    public string MensajeErrorNueva =>
        !string.IsNullOrEmpty(NuevaContrasena) && NuevaContrasena.Length < 6
            ? "La nueva contraseña debe tener al menos 6 caracteres."
            : string.Empty;

    public string MensajeErrorConfirmar =>
        !string.IsNullOrEmpty(ConfirmarContrasena) && !string.Equals(ConfirmarContrasena, NuevaContrasena, StringComparison.Ordinal)
            ? "Las contraseñas no coinciden."
            : string.Empty;

    public event EventHandler? SolicitarCierre;

    public PasswordRecoveryViewModel(IAuthService authService, IDialogService dialogService)
    {
        _authService = authService;
        _dialogService = dialogService;
    }

    private bool PuedeRestablecer() =>
        !IsBusy
        && !string.IsNullOrEmpty(CodigoRecuperacion)
        && NuevaContrasena.Length >= 6
        && string.Equals(NuevaContrasena, ConfirmarContrasena, StringComparison.Ordinal);

    [RelayCommand(CanExecute = nameof(PuedeRestablecer))]
    private async Task RestablecerAsync()
    {
        MensajeError = string.Empty;
        IsBusy = true;
        try
        {
            var codigoNuevo = await _authService.RestablecerContrasenaConCodigoAsync(
                CodigoRecuperacion.ToCharArray(), NuevaContrasena.ToCharArray());

            RestablecidoExitoso = true;
            CodigoNuevo = codigoNuevo;
            MostrandoCodigoNuevo = true;

            CodigoRecuperacion = string.Empty;
            NuevaContrasena = string.Empty;
            ConfirmarContrasena = string.Empty;
        }
        catch (ServiceException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception)
        {
            MensajeError = "Ocurrió un error inesperado. Intente nuevamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopiarCodigo()
    {
        if (!string.IsNullOrEmpty(CodigoNuevo))
        {
            _dialogService.CopyToClipboard(CodigoNuevo);
        }
    }

    private bool PuedeCerrar() => !MostrandoCodigoNuevo || ConfirmoQueGuardeElCodigo;

    [RelayCommand(CanExecute = nameof(PuedeCerrar))]
    private void Cerrar()
    {
        // El código nuevo ya se mostró y se confirmó que se guardó: no debe quedar
        // retenido en memoria más tiempo del necesario.
        CodigoNuevo = string.Empty;
        SolicitarCierre?.Invoke(this, EventArgs.Empty);
    }
}
