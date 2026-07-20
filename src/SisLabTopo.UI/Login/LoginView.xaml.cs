using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SisLabTopo.UI.Login;

/// <summary>
/// Vista de acceso administrativo. El DataContext (<see cref="LoginViewModel"/>) es
/// asignado externamente por <see cref="Navigation.NavigationService"/>.
///
/// Única excepción de "code-behind" del patrón MVVM de esta pantalla, documentada
/// deliberadamente en dos puntos:
/// <list type="bullet">
/// <item><description>
/// <b>Lectura de la contraseña:</b> <see cref="System.Windows.Controls.PasswordBox.Password"/>
/// no es una <c>DependencyProperty</c> por diseño de seguridad de WPF (para no dejar la
/// contraseña accesible desde el árbol de bindings/estilos). Se lee aquí, solo en el
/// instante del submit, se copia a un <c>char[]</c> y se pasa como
/// <c>CommandParameter</c> a <see cref="LoginViewModel.IniciarSesionCommand"/>; el
/// PasswordBox se limpia inmediatamente después. Limitación conocida y aceptada: a
/// diferencia de <c>char[]</c> en Java (que se puede sobrescribir byte a byte),
/// <see cref="System.Windows.Controls.PasswordBox.Password"/> devuelve un
/// <c>string</c> inmutable que permanece en el heap gestionado hasta que el recolector
/// de basura lo recicle -- no hay forma soportada de "borrarlo" de forma determinista
/// en WPF; el <c>char[]</c> que sí construimos nosotros se limpia de forma determinista
/// dentro de <c>AuthService</c> tras usarlo.
/// </description></item>
/// <item><description>
/// <b>Cuenta regresiva de bloqueo:</b> el <see cref="DispatcherTimer"/> de 1s vive aquí
/// (no en el ViewModel) para no acoplar <see cref="LoginViewModel"/> a un
/// <see cref="Dispatcher"/> real de WPF y mantenerlo así trivialmente testeable sin UI
/// (ver <c>SisLabTopo.UI.Tests</c>). Se detiene en <see cref="Window.Closed"/> para no
/// seguir llamando al servicio tras navegar fuera de esta pantalla (equivalente a
/// <c>LoginPanel.detach()</c> en Java).
/// </description></item>
/// </list>
/// </summary>
public partial class LoginView : Window
{
    private readonly DispatcherTimer _lockoutTimer;

    public LoginView()
    {
        InitializeComponent();

        _lockoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _lockoutTimer.Tick += async (_, _) => await ActualizarEstadoBloqueoAsync();

        Loaded += LoginView_Loaded;
        Closed += LoginView_Closed;
    }

    private async void LoginView_Loaded(object sender, RoutedEventArgs e)
    {
        PasswordInput.Focus();
        await ActualizarEstadoBloqueoAsync();
        _lockoutTimer.Start();
    }

    private void LoginView_Closed(object? sender, EventArgs e) => _lockoutTimer.Stop();

    private async Task ActualizarEstadoBloqueoAsync()
    {
        if (DataContext is LoginViewModel vm)
        {
            await vm.ActualizarEstadoBloqueoAsync();
        }
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            EjecutarLogin();
        }
    }

    private void BtnIngresar_Click(object sender, RoutedEventArgs e) => EjecutarLogin();

    private void EjecutarLogin()
    {
        if (DataContext is not LoginViewModel vm)
        {
            return;
        }

        var contrasena = PasswordInput.Password.ToCharArray();
        PasswordInput.Clear();

        if (vm.IniciarSesionCommand.CanExecute(contrasena))
        {
            vm.IniciarSesionCommand.Execute(contrasena);
        }
    }
}
