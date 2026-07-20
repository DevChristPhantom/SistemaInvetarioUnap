using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SisLabTopo.UI.Dashboard;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Navigation;

/// <summary>
/// Implementación de <see cref="INavigationService"/>. Distingue dos casos según el tipo
/// del ViewModel resuelto:
/// <list type="bullet">
/// <item><description>
/// <see cref="LoginViewModel"/> / <see cref="ShellViewModel"/>: son ventanas raíz.
/// Se crea (vía DI) la ventana correspondiente, se le asigna el ViewModel como
/// <see cref="FrameworkElement.DataContext"/>, se muestra, y se cierra la ventana raíz
/// anterior (si había una) -- replica el flujo de <c>MainFrame.mostrarLogin()</c> /
/// <c>mostrarWorkspace()</c> en Java, pero con ventanas WPF reales en vez de un
/// <c>CardLayout</c> compartido.
/// </description></item>
/// <item><description>
/// Cualquier <see cref="IShellContentViewModel"/> (Dashboard/Equipos/Préstamos/
/// Historial/Configuración, hoy placeholders de Fase 4): se asigna a
/// <see cref="ShellViewModel.CurrentViewModel"/>, sin tocar ninguna ventana -- el
/// <c>ContentControl</c> del Shell resuelve la vista correspondiente vía DataTemplate.
/// </description></item>
/// </list>
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NavigationService> _logger;
    private Window? _currentRootWindow;
    private ShellViewModel? _shellViewModel;

    public NavigationService(IServiceProvider serviceProvider, ILogger<NavigationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class => NavigateTo(typeof(TViewModel));

    public void NavigateTo(Type viewModelType)
    {
        var viewModel = _serviceProvider.GetRequiredService(viewModelType);
        _logger.LogInformation("Navegando a {ViewModel}.", viewModelType.Name);

        switch (viewModel)
        {
            case ShellViewModel shellViewModel:
                ShowRootWindow<Shell.ShellView>(shellViewModel);
                _shellViewModel = shellViewModel;
                // Destino inicial del Shell al iniciar sesión, equivalente a
                // MainFrame.mostrarWorkspace() mostrando siempre "DASHBOARD" primero.
                NavigateTo<DashboardViewModel>();
                break;

            case LoginViewModel loginViewModel:
                _shellViewModel = null;
                ShowRootWindow<Login.LoginView>(loginViewModel);
                break;

            case IShellContentViewModel contentViewModel:
                if (_shellViewModel is null)
                {
                    throw new InvalidOperationException(
                        $"No se puede navegar a {viewModelType.Name}: el Shell todavía no está activo.");
                }

                _shellViewModel.CurrentViewModel = contentViewModel;
                break;

            default:
                throw new InvalidOperationException(
                    $"NavigationService no sabe cómo navegar a '{viewModelType.Name}': no es una ventana raíz conocida ni implementa IShellContentViewModel.");
        }
    }

    private void ShowRootWindow<TWindow>(object viewModel) where TWindow : Window
    {
        var window = _serviceProvider.GetRequiredService<TWindow>();
        window.DataContext = viewModel;

        var previousWindow = _currentRootWindow;

        Application.Current.MainWindow = window;
        window.Show();
        _currentRootWindow = window;

        previousWindow?.Close();
    }
}
