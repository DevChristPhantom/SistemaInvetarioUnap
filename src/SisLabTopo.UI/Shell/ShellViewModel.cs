using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Services;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;

namespace SisLabTopo.UI.Shell;

/// <summary>
/// ViewModel de la ventana principal post-login. Puerto funcional de
/// <c>MainFrame</c> (Java): sidebar de navegación (5 destinos) + barra superior con
/// "Cerrar sesión" + barra de estado inferior ("Equipos Disponibles: X de Y", igual que
/// <c>MainFrame.actualizarBarraEstado()</c>). El contenido central (<see cref="CurrentViewModel"/>)
/// se resuelve completamente a través de <see cref="INavigationService"/>: por ahora
/// apunta a las 5 <c>PlaceholderView</c> de la Fase 4; la Fase 5 solo tiene que registrar
/// las vistas/viewmodels reales y este ViewModel no cambia.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IEquipoService _equipoService;
    private readonly ILogger<ShellViewModel> _logger;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private string _estadoEquiposTexto = "Cargando equipos disponibles...";

    public ShellViewModel(INavigationService navigationService, IEquipoService equipoService, ILogger<ShellViewModel> logger)
    {
        _navigationService = navigationService;
        _equipoService = equipoService;
        _logger = logger;
    }

    [RelayCommand]
    private void NavegarDashboard() => Navegar<DashboardViewModel>();

    [RelayCommand]
    private void NavegarEquipos() => Navegar<EquiposViewModel>();

    [RelayCommand]
    private void NavegarPrestamos() => Navegar<PrestamosViewModel>();

    [RelayCommand]
    private void NavegarHistorial() => Navegar<HistorialViewModel>();

    [RelayCommand]
    private void NavegarConfiguracion() => Navegar<ConfiguracionViewModel>();

    [RelayCommand]
    private void CerrarSesion()
    {
        _logger.LogInformation("Cierre de sesión solicitado desde el Shell.");
        _navigationService.NavigateTo<LoginViewModel>();
    }

    private void Navegar<TViewModel>() where TViewModel : class, IShellContentViewModel
    {
        _navigationService.NavigateTo<TViewModel>();
        _ = ActualizarBarraEstadoAsync();
    }

    /// <summary>
    /// Refresca el texto de la barra de estado inferior, equivalente a
    /// <c>MainFrame.actualizarBarraEstado()</c>. Se tolera cualquier error de lectura
    /// (igual que <see cref="SisLabTopo.Services.IEquipoService"/> ya hace internamente)
    /// dejando un texto neutro en vez de propagar la excepción a la UI.
    /// </summary>
    public async Task ActualizarBarraEstadoAsync()
    {
        try
        {
            var disponibles = await _equipoService.ObtenerDisponiblesAsync();
            var total = await _equipoService.ObtenerTodosAsync();
            EstadoEquiposTexto = $"Equipos Disponibles: {disponibles.Count} de {total.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar la barra de estado de equipos.");
            EstadoEquiposTexto = "Equipos Disponibles: --";
        }
    }
}
