using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Configuracion;

/// <summary>
/// ViewModel del panel de Configuración. Puerto funcional de
/// <c>MainFrame.crearConfiguracionPanel()</c> (Java, inline en <c>MainFrame</c> -- aquí
/// pantalla propia): cambio de contraseña de administrador estando ya autenticado.
///
/// Nota de alcance (Fase 5b): el asistente de primer arranque y la recuperación de
/// contraseña sin "admin123" hardcodeado son de la Fase 6 -- aquí solo el cambio de
/// contraseña de un administrador ya logueado.
///
/// Mejora deliberada sobre Java: validación inline reactiva (mínimo 6 caracteres,
/// coincidencia de confirmación) mostrada como texto bajo cada campo, en vez de un único
/// <c>JOptionPane</c> bloqueante que solo aparecía DESPUÉS de enviar el formulario.
///
/// Nota sobre <c>PasswordBox</c>: igual que documenta <c>LoginViewModel</c>, WPF no
/// permite bindear <see cref="System.Windows.Controls.PasswordBox.Password"/> de forma
/// nativa. Aquí, a diferencia de Login (que solo lee la contraseña en el instante del
/// submit), se necesita el valor MIENTRAS el usuario escribe para poder mostrar la
/// validación inline -- así que <c>ConfiguracionView</c> reenvía cada
/// <c>PasswordChanged</c> a estas tres propiedades de cadena (misma excepción de
/// code-behind ya documentada para <c>PasswordBox</c>, solo que atada a cada tecleo en
/// vez de únicamente al envío).
/// </summary>
public partial class ConfiguracionViewModel : ObservableObject, IShellContentViewModel
{
    private readonly IAuthService _authService;
    private readonly ILogger<ConfiguracionViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string _contrasenaActual = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeErrorNueva))]
    [NotifyPropertyChangedFor(nameof(MensajeErrorConfirmar))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string _nuevaContrasena = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeErrorConfirmar))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string _confirmarContrasena = string.Empty;

    [ObservableProperty]
    private string _mensajeExito = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private bool _isBusy;

    /// <summary>Validación inline: solo se muestra una vez que el usuario empezó a escribir (cadena vacía = sin mensaje todavía).</summary>
    public string MensajeErrorNueva =>
        !string.IsNullOrEmpty(NuevaContrasena) && NuevaContrasena.Length < 6
            ? "La nueva contraseña debe tener al menos 6 caracteres."
            : string.Empty;

    public string MensajeErrorConfirmar =>
        !string.IsNullOrEmpty(ConfirmarContrasena) && !string.Equals(ConfirmarContrasena, NuevaContrasena, StringComparison.Ordinal)
            ? "Las contraseñas no coinciden."
            : string.Empty;

    public ConfiguracionViewModel(IAuthService authService, ILogger<ConfiguracionViewModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    private bool PuedeGuardar() =>
        !IsBusy
        && !string.IsNullOrEmpty(ContrasenaActual)
        && NuevaContrasena.Length >= 6
        && string.Equals(NuevaContrasena, ConfirmarContrasena, StringComparison.Ordinal);

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        MensajeError = string.Empty;
        MensajeExito = string.Empty;

        IsBusy = true;
        try
        {
            await _authService.CambiarContrasenaAsync(ContrasenaActual.ToCharArray(), NuevaContrasena.ToCharArray());
            MensajeExito = "Contraseña actualizada con éxito.";
            ContrasenaActual = string.Empty;
            NuevaContrasena = string.Empty;
            ConfirmarContrasena = string.Empty;
        }
        catch (ServiceException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo inesperado al cambiar la contraseña de administrador.");
            MensajeError = "Ocurrió un error inesperado. Intente nuevamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
