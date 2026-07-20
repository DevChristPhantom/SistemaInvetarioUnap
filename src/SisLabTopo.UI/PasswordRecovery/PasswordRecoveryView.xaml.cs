using System.ComponentModel;
using System.Windows;

namespace SisLabTopo.UI.PasswordRecovery;

/// <summary>
/// Vista del diálogo de recuperación de contraseña. Code-behind (misma excepción
/// documentada en <c>LoginView</c>/<c>ConfiguracionView</c>): reenviar cada
/// <c>PasswordChanged</c> de los 2 <see cref="System.Windows.Controls.PasswordBox"/> al
/// ViewModel (necesario para la validación inline reactiva), limpiarlos cuando el
/// restablecimiento se completa con éxito, y cerrar la ventana cuando el ViewModel pide
/// <c>SolicitarCierre</c> (mismo patrón que el resto de diálogos de la Fase 5, p.ej.
/// <c>DevolucionView</c>).
/// </summary>
public partial class PasswordRecoveryView : Window
{
    public PasswordRecoveryView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is PasswordRecoveryViewModel anterior)
            {
                anterior.PropertyChanged -= OnViewModelPropertyChanged;
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is PasswordRecoveryViewModel nuevo)
            {
                nuevo.PropertyChanged += OnViewModelPropertyChanged;
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PasswordRecoveryViewModel.MostrandoCodigoNuevo))
        {
            return;
        }

        if (sender is PasswordRecoveryViewModel { MostrandoCodigoNuevo: true })
        {
            PasswordNueva.Clear();
            PasswordConfirmar.Clear();
        }
    }

    private void OnSolicitarCierre(object? sender, EventArgs e)
    {
        DialogResult = sender is PasswordRecoveryViewModel { RestablecidoExitoso: true };
    }

    private void PasswordNueva_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PasswordRecoveryViewModel vm)
        {
            vm.NuevaContrasena = PasswordNueva.Password;
        }
    }

    private void PasswordConfirmar_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PasswordRecoveryViewModel vm)
        {
            vm.ConfirmarContrasena = PasswordConfirmar.Password;
        }
    }
}
