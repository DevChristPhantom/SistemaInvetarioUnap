using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;

namespace SisLabTopo.UI.Startup;

/// <summary>
/// ViewModel del asistente de primer arranque (Fase 6). Reemplaza por completo el hueco
/// de seguridad heredado de la versión Java: una contraseña de administrador
/// ("admin123") hardcodeada en el código y anunciada en texto plano por el instalador
/// (<c>installer/setup.iss</c>). <see cref="App.OnStartup"/> navega aquí -- en vez de a
/// <see cref="LoginViewModel"/> -- cuando <see cref="IAuthService.ExisteContrasenaConfiguradaAsync"/>
/// devuelve <c>false</c> (base de datos nueva, sin ninguna contraseña configurada
/// todavía).
///
/// Dos pasos en la misma ventana (misma identidad visual institucional que
/// <see cref="Login.LoginView"/>, reutilizando los mismos <c>Themes/</c>):
/// <list type="number">
/// <item><description>
/// <see cref="MostrandoCodigo"/> == <c>false</c>: el administrador define su contraseña
/// (mínimo 6 caracteres, con confirmación) -- validación inline reactiva, mismo patrón
/// que <c>ConfiguracionViewModel</c>.
/// </description></item>
/// <item><description>
/// <see cref="MostrandoCodigo"/> == <c>true</c>: se muestra el código de recuperación
/// generado UNA SOLA VEZ, con advertencia explícita de que no volverá a mostrarse y un
/// botón para copiarlo al portapapeles. Solo tras marcar la casilla
/// <see cref="ConfirmoQueGuardeElCodigo"/> se habilita continuar al login normal --
/// evita que alguien avance sin haber tenido oportunidad de guardar el código.
/// </description></item>
/// </list>
///
/// Higiene de memoria: el <c>char[]</c> de la contraseña se limpia (vía
/// <see cref="IAuthService.ConfigurarContrasenaInicialAsync"/>, que lo hace
/// internamente) y <see cref="CodigoRecuperacion"/> se vacía explícitamente en cuanto el
/// administrador continúa al Login -- no debe quedar accesible por la UI una vez
/// mostrado.
/// </summary>
public partial class FirstRunSetupViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<FirstRunSetupViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeErrorNueva))]
    [NotifyPropertyChangedFor(nameof(MensajeErrorConfirmar))]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string _nuevaContrasena = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeErrorConfirmar))]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private string _confirmarContrasena = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearCuentaCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _mostrandoCodigo;

    [ObservableProperty]
    private string _codigoRecuperacion = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinuarCommand))]
    private bool _confirmoQueGuardeElCodigo;

    /// <summary>Validación inline: solo se muestra una vez que el usuario empezó a escribir.</summary>
    public string MensajeErrorNueva =>
        !string.IsNullOrEmpty(NuevaContrasena) && NuevaContrasena.Length < 6
            ? "La contraseña debe tener al menos 6 caracteres."
            : string.Empty;

    public string MensajeErrorConfirmar =>
        !string.IsNullOrEmpty(ConfirmarContrasena) && !string.Equals(ConfirmarContrasena, NuevaContrasena, StringComparison.Ordinal)
            ? "Las contraseñas no coinciden."
            : string.Empty;

    public FirstRunSetupViewModel(
        IAuthService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<FirstRunSetupViewModel> logger)
    {
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
    }

    private bool PuedeCrearCuenta() =>
        !IsBusy
        && NuevaContrasena.Length >= 6
        && string.Equals(NuevaContrasena, ConfirmarContrasena, StringComparison.Ordinal);

    [RelayCommand(CanExecute = nameof(PuedeCrearCuenta))]
    private async Task CrearCuentaAsync()
    {
        MensajeError = string.Empty;
        IsBusy = true;
        try
        {
            var codigo = await _authService.ConfigurarContrasenaInicialAsync(NuevaContrasena.ToCharArray());

            CodigoRecuperacion = codigo;
            MostrandoCodigo = true;
            NuevaContrasena = string.Empty;
            ConfirmarContrasena = string.Empty;
            _logger.LogInformation("Asistente de primer arranque completado: cuenta de administrador creada.");
        }
        catch (ServiceException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo inesperado en el asistente de primer arranque.");
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
        if (!string.IsNullOrEmpty(CodigoRecuperacion))
        {
            _dialogService.CopyToClipboard(CodigoRecuperacion);
        }
    }

    private bool PuedeContinuar() => ConfirmoQueGuardeElCodigo;

    [RelayCommand(CanExecute = nameof(PuedeContinuar))]
    private void Continuar()
    {
        // El código ya se mostró y el administrador confirmó haberlo guardado: no debe
        // quedar retenido en memoria más tiempo del necesario.
        CodigoRecuperacion = string.Empty;
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
