using System.ComponentModel;
using System.Windows;

namespace SisLabTopo.UI.Startup;

/// <summary>
/// Vista del asistente de primer arranque. Code-behind (misma excepción documentada que
/// <c>LoginView</c>/<c>ConfiguracionView</c>): <see cref="System.Windows.Controls.PasswordBox.Password"/>
/// no es una <c>DependencyProperty</c>, así que cada <c>PasswordChanged</c> se reenvía a
/// la propiedad de cadena correspondiente del ViewModel -- necesario aquí (a diferencia
/// de Login) para poder mostrar la validación inline reactiva mientras el administrador
/// escribe, igual que <c>ConfiguracionView</c>. También limpia ambos <c>PasswordBox</c>
/// en cuanto se pasa al paso 2 (el ViewModel ya vacía sus propias propiedades de cadena,
/// pero no puede "ver" ni limpiar el control real).
/// </summary>
public partial class FirstRunSetupView : Window
{
    public FirstRunSetupView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is FirstRunSetupViewModel anterior)
            {
                anterior.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (e.NewValue is FirstRunSetupViewModel nuevo)
            {
                nuevo.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FirstRunSetupViewModel.MostrandoCodigo))
        {
            return;
        }

        if (sender is FirstRunSetupViewModel { MostrandoCodigo: true })
        {
            PasswordNueva.Clear();
            PasswordConfirmar.Clear();
        }
    }

    private void PasswordNueva_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is FirstRunSetupViewModel vm)
        {
            vm.NuevaContrasena = PasswordNueva.Password;
        }
    }

    private void PasswordConfirmar_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is FirstRunSetupViewModel vm)
        {
            vm.ConfirmarContrasena = PasswordConfirmar.Password;
        }
    }
}
