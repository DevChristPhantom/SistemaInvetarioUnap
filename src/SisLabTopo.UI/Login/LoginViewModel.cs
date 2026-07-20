using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.PasswordRecovery;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Login;

/// <summary>
/// ViewModel de la pantalla de acceso administrativo. Puerto funcional de
/// <c>LoginPanel</c> (Java): un único campo de contraseña, bloqueo de 3 intentos
/// fallidos con cuenta regresiva de 30s (persistido en base de datos vía
/// <see cref="IAuthService"/>, a diferencia de Java que lo perdía al reiniciar la app),
/// y navegación al Shell tras autenticar con éxito.
///
/// Nota sobre el <c>PasswordBox</c>: <see cref="System.Windows.Controls.PasswordBox.Password"/>
/// no es una <c>DependencyProperty</c> (por diseño, para no dejar la contraseña en texto
/// plano en el árbol de bindings de WPF), así que no puede bindearse de forma nativa a
/// <see cref="ObservableObject"/>. La técnica elegida aquí es la recomendada para MVVM
/// puro con WPF: el code-behind de <c>LoginView</c> (única excepción de "code-behind"
/// permitida, documentada explícitamente) lee <c>PasswordBox.Password</c> solo en el
/// momento del submit, lo convierte a <c>char[]</c>, y lo pasa como
/// <c>CommandParameter</c> de <see cref="IniciarSesionCommand"/> -- el ViewModel nunca
/// referencia el control ni mantiene la contraseña como propiedad bindeada de cadena.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private bool _isLocked;

    [ObservableProperty]
    private long _segundosBloqueo;

    public LoginViewModel(
        IAuthService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
    }

    private bool PuedeIniciarSesion() => !IsBusy && !IsLocked;

    [RelayCommand(CanExecute = nameof(PuedeIniciarSesion))]
    private async Task IniciarSesionAsync(char[]? contrasena)
    {
        if (contrasena is null || contrasena.Length == 0)
        {
            ErrorMessage = "Por favor ingrese la contraseña.";
            return;
        }

        IsBusy = true;
        try
        {
            var autenticado = await _authService.VerificarContrasenaAsync(contrasena);
            if (autenticado)
            {
                ErrorMessage = string.Empty;
                _logger.LogInformation("Inicio de sesión exitoso. Navegando al Shell.");
                _navigationService.NavigateTo<ShellViewModel>();
                return;
            }

            await ActualizarEstadoBloqueoAsync();
            if (!IsLocked)
            {
                var intentos = await _authService.ObtenerIntentosFallidosAsync();
                ErrorMessage = $"Contraseña incorrecta. Intento {intentos} de 3.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Abre el diálogo de recuperación de contraseña (Fase 6): reemplaza el enlace
    /// "¿Olvidó su contraseña?" -- placeholder visual desde la Fase 4, sin cablear a
    /// ningún flujo real hasta ahora -- por el diálogo real de
    /// <see cref="PasswordRecoveryViewModel"/>. Se construye directamente con
    /// <c>new</c> (mismo patrón de todos los diálogos de la Fase 5: ver el comentario
    /// XML de <see cref="IDialogService"/>), pasándole los mismos servicios que
    /// <see cref="LoginViewModel"/> ya tiene inyectados.
    /// </summary>
    [RelayCommand]
    private void AbrirRecuperacion()
    {
        var recoveryViewModel = new PasswordRecoveryViewModel(_authService, _dialogService);
        _dialogService.ShowDialog(recoveryViewModel);
    }

    /// <summary>
    /// Consulta el estado de bloqueo actual y actualiza <see cref="IsLocked"/>/
    /// <see cref="SegundosBloqueo"/>/<see cref="ErrorMessage"/> en consecuencia. Se
    /// invoca al cargar la vista (para reflejar un bloqueo que ya estaba vigente antes
    /// de abrir la pantalla -- posible porque ahora el bloqueo se persiste en base de
    /// datos) y desde el <c>DispatcherTimer</c> de 1s que posee <c>LoginView</c>
    /// (deliberadamente fuera del ViewModel para no acoplarlo a WPF y mantenerlo
    /// trivialmente testeable sin un Dispatcher real).
    /// </summary>
    public async Task ActualizarEstadoBloqueoAsync()
    {
        var segundos = await _authService.ObtenerSegundosBloqueoAsync();
        SegundosBloqueo = segundos;

        if (segundos > 0)
        {
            IsLocked = true;
            ErrorMessage = $"Cuenta bloqueada por {segundos} segundos por múltiples intentos fallidos.";
        }
        else if (IsLocked)
        {
            // El bloqueo que estaba vigente acaba de expirar (detectado por el timer de LoginView).
            IsLocked = false;
            ErrorMessage = string.Empty;
        }
    }
}
