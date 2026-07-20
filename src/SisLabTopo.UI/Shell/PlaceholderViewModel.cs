using CommunityToolkit.Mvvm.ComponentModel;

namespace SisLabTopo.UI.Shell;

/// <summary>
/// ViewModel de relleno para las 5 pantallas de negocio que todavía no existen (Fase 5):
/// Dashboard, Equipos, Préstamos, Historial, Configuración. Solo expone un título y un
/// mensaje fijo que <c>PlaceholderView</c> muestra centrado. Cada pantalla tiene su
/// propia subclase concreta (<see cref="DashboardViewModel"/>, etc.) en vez de navegar
/// todas a una única instancia compartida: así <see cref="Navigation.NavigationService"/>
/// puede resolver "el ViewModel de Equipos" por tipo (<c>NavigateTo&lt;EquiposViewModel&gt;()</c>)
/// exactamente con la misma API que usará la Fase 5 cuando estas subclases se conviertan
/// en ViewModels reales -- el Shell y la navegación no cambian.
/// </summary>
public abstract partial class PlaceholderViewModel : ObservableObject, IShellContentViewModel
{
    public string Titulo { get; }

    public string Mensaje => $"{Titulo} — pendiente Fase 5";

    protected PlaceholderViewModel(string titulo)
    {
        Titulo = titulo;
    }
}

public sealed class DashboardViewModel() : PlaceholderViewModel("Dashboard")
{
}

public sealed class EquiposViewModel() : PlaceholderViewModel("Equipos")
{
}

public sealed class PrestamosViewModel() : PlaceholderViewModel("Préstamos Activos")
{
}

public sealed class HistorialViewModel() : PlaceholderViewModel("Historial")
{
}

public sealed class ConfiguracionViewModel() : PlaceholderViewModel("Configuración")
{
}
