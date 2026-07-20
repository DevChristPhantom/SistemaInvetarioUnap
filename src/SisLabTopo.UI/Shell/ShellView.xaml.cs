using System.Windows;

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
    }

    private async void ShellView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
        {
            await vm.ActualizarBarraEstadoAsync();
        }
    }
}
