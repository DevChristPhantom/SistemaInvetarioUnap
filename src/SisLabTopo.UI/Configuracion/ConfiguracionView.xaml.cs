using System.ComponentModel;
using System.Windows.Controls;

namespace SisLabTopo.UI.Configuracion;

/// <summary>
/// Vista de Configuración. Code-behind (documentado, misma excepción que
/// <c>LoginView</c>): reenviar cada <c>PasswordChanged</c> de los 3
/// <see cref="PasswordBox"/> a las propiedades de cadena del ViewModel (necesario para
/// la validación inline reactiva), y limpiar los 3 controles cuando el cambio de
/// contraseña se completa con éxito (el ViewModel ya vacía sus propias propiedades,
/// pero no puede "ver" ni limpiar el <see cref="PasswordBox"/> real).
/// </summary>
public partial class ConfiguracionView : UserControl
{
    public ConfiguracionView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ConfiguracionViewModel anterior)
            {
                anterior.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (e.NewValue is ConfiguracionViewModel nuevo)
            {
                nuevo.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConfiguracionViewModel.MensajeExito))
        {
            return;
        }

        if (sender is ConfiguracionViewModel { MensajeExito.Length: > 0 })
        {
            PasswordActual.Clear();
            PasswordNueva.Clear();
            PasswordConfirmar.Clear();
        }
    }

    private void PasswordActual_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConfiguracionViewModel vm)
        {
            vm.ContrasenaActual = PasswordActual.Password;
        }
    }

    private void PasswordNueva_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConfiguracionViewModel vm)
        {
            vm.NuevaContrasena = PasswordNueva.Password;
        }
    }

    private void PasswordConfirmar_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConfiguracionViewModel vm)
        {
            vm.ConfirmarContrasena = PasswordConfirmar.Password;
        }
    }
}
