namespace SisLabTopo.UI.Navigation;

/// <summary>
/// Servicio de navegación de la aplicación, resuelto vía inyección de dependencias.
/// Es agnóstico de "dónde" vive cada ViewModel de destino (ventana raíz -- Login/Shell --
/// o contenido dentro del Shell): la implementación decide, según el tipo concreto de
/// ViewModel, si debe reemplazar la ventana principal o solo el contenido actual del
/// Shell (ver <see cref="Shell.IShellContentViewModel"/>). Así, tanto
/// <c>LoginViewModel.NavigateTo&lt;ShellViewModel&gt;()</c> como
/// <c>ShellViewModel.NavigateTo&lt;DashboardViewModel&gt;()</c> usan exactamente la misma
/// API.
/// </summary>
public interface INavigationService
{
    /// <summary>Navega al ViewModel de tipo <typeparamref name="TViewModel"/>, resuelto desde el contenedor de DI.</summary>
    void NavigateTo<TViewModel>() where TViewModel : class;

    /// <summary>Navega al ViewModel del tipo dado, resuelto desde el contenedor de DI.</summary>
    void NavigateTo(Type viewModelType);
}
