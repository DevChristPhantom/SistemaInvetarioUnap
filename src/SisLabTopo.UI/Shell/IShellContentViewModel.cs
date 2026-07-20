namespace SisLabTopo.UI.Shell;

/// <summary>
/// Interfaz marcadora que identifica un ViewModel como "contenido de pantalla" alojado
/// dentro de <see cref="ShellViewModel.CurrentViewModel"/> (Dashboard, Equipos,
/// Préstamos, Historial, Configuración -- y, por ahora, sus <c>PlaceholderViewModel</c>
/// de la Fase 4). <see cref="Navigation.NavigationService"/> la usa para distinguir
/// "navegar dentro del Shell" de "navegar a una ventana raíz" (Login/Shell) sin
/// necesitar una lista de tipos hardcodeada.
/// </summary>
public interface IShellContentViewModel
{
}
