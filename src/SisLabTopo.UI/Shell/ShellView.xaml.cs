using System.Windows;
using System.Windows.Controls;

namespace SisLabTopo.UI.Shell;

/// <summary>
/// Ventana raíz post-login (workspace). El DataContext (<see cref="ShellViewModel"/>) es
/// asignado externamente por <see cref="Navigation.NavigationService"/> tras resolver
/// esta ventana desde el contenedor de DI.
/// </summary>
public partial class ShellView : Window
{
    public ShellView()
    {
        InitializeComponent();
        Loaded += ShellView_Loaded;
        Closed += ShellView_Closed;
    }

    private async void ShellView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
        {
            await vm.ActualizarBarraEstadoAsync();
        }
    }

    /// <summary>
    /// Red de seguridad para detener el DispatcherTimer del reloj del header si la
    /// ventana se cierra por una vía distinta a "Cerrar sesión" (p.ej. Alt+F4) --
    /// ShellViewModel.CerrarSesion ya lo detiene en el flujo normal.
    /// </summary>
    private void ShellView_Closed(object? sender, EventArgs e)
    {
        if (DataContext is ShellViewModel vm)
        {
            vm.DetenerReloj();
        }
    }

    /// <summary>
    /// Abre el menú desplegable del avatar de usuario (con "Cerrar sesión" adentro,
    /// ver ShellView.xaml) -- decisión de diseño de esta fase: en vez del antiguo botón
    /// grande rojo "Cerrar sesión" en el header, el cierre de sesión ahora vive dentro
    /// de este menú contextual anclado al bloque de usuario/avatar, un patrón estándar
    /// de Microsoft 365 / Windows 11.
    /// </summary>
    private void AvatarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elemento && elemento.ContextMenu is ContextMenu menu)
        {
            menu.PlacementTarget = elemento;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
