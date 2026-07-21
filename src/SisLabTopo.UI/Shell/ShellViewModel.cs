using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Services;
using SisLabTopo.UI.Configuracion;
using SisLabTopo.UI.Dashboard;
using SisLabTopo.UI.Equipos;
using SisLabTopo.UI.Historial;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.Prestamos;
using SisLabTopo.UI.Theming;

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
    private readonly IThemeService _themeService;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly DispatcherTimer _relojTimer;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private string _estadoEquiposTexto = "Cargando equipos disponibles...";

    /// <summary>
    /// Header estilo Microsoft 365: fecha y hora en vivo, actualizada cada segundo por
    /// <see cref="_relojTimer"/>. Puerto de UX nuevo (Java/MainFrame no tenía reloj en
    /// el header) -- formato "lun., 20 jul. 2026 · 14:35" (cultura es-PE del sistema).
    /// </summary>
    [ObservableProperty]
    private string _fechaHoraTexto = string.Empty;

    /// <summary>
    /// Nombre de usuario mostrado en el header. La app solo tiene el rol de
    /// "Administrador" (sin tabla de usuarios múltiples, ver Domain/Services ya
    /// congelados), así que este valor es fijo -- no hardcodear un nombre de persona
    /// inventado.
    /// </summary>
    public string NombreUsuario => "Administrador";

    /// <summary>Iniciales para el avatar circular del header (sin foto de usuario real).</summary>
    public string InicialesUsuario => "AD";

    [ObservableProperty]
    private bool _esTemaOscuro;

    public ShellViewModel(
        INavigationService navigationService,
        IEquipoService equipoService,
        IThemeService themeService,
        ILogger<ShellViewModel> logger)
    {
        _navigationService = navigationService;
        _equipoService = equipoService;
        _themeService = themeService;
        _logger = logger;

        EsTemaOscuro = _themeService.TemaActual == AppTheme.Oscuro;

        ActualizarFechaHora();
        _relojTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _relojTimer.Tick += (_, _) => ActualizarFechaHora();
        _relojTimer.Start();
    }

    private void ActualizarFechaHora()
    {
        // "dddd, d 'de' MMMM 'de' yyyy" sería demasiado largo para el header; se usa un
        // formato corto y elegante en español: "lun. 20 jul. 2026 · 14:35".
        var ahora = DateTime.Now;
        FechaHoraTexto = $"{ahora:ddd dd MMM yyyy} · {ahora:HH:mm}";
    }

    [RelayCommand]
    private void AlternarTema()
    {
        _themeService.AlternarTema();
        EsTemaOscuro = _themeService.TemaActual == AppTheme.Oscuro;
    }

    /// <summary>
    /// Detiene el reloj del header. Se llama explícitamente al cerrar sesión (ver
    /// <see cref="CerrarSesion"/>) y también desde <c>ShellView.Closed</c> como red de
    /// seguridad -- este ViewModel es Transient (una instancia nueva por cada login,
    /// ver ServiceRegistration), así que sin esto cada sesión dejaría un
    /// DispatcherTimer huérfano corriendo indefinidamente en segundo plano.
    /// </summary>
    public void DetenerReloj() => _relojTimer.Stop();

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
        DetenerReloj();
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
