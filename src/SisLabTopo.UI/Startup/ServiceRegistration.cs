using Microsoft.Extensions.DependencyInjection;
using SisLabTopo.UI.Configuracion;
using SisLabTopo.UI.Dashboard;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Equipos;
using SisLabTopo.UI.Historial;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.Prestamos;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Startup;

/// <summary>
/// Registro en el contenedor de DI de todo lo que aporta la capa de presentación:
/// servicio de navegación, ventanas raíz (Login/Shell) y ViewModels. Sigue el mismo
/// patrón que <c>SisLabTopo.Data.ServiceCollectionExtensions.AddSisLabTopoData()</c>,
/// <c>SisLabTopo.Services.ServiceCollectionExtensions.AddSisLabTopoServices()</c> y
/// <c>SisLabTopo.Reports.ServiceCollectionExtensions.AddSisLabTopoReports()</c> ya
/// existentes -- no se encontró ningún <c>AddSisLabTopoUi()</c> previo, así que se añade
/// aquí, dentro de <c>SisLabTopo.UI</c> (no en un proyecto compartido, ya que solo
/// <see cref="App"/> lo consume).
///
/// Nota de ciclo de vida: <c>SisLabTopo.Data</c>/<c>SisLabTopo.Services</c> registran su
/// <c>DbContext</c> y servicios de negocio como <em>Scoped</em> (patrón estándar de EF
/// Core). Esta app de escritorio no tiene el concepto de "petición HTTP" que
/// normalmente delimita un scope, así que <see cref="App"/> crea un único
/// <see cref="IServiceScope"/> para toda la vida de la aplicación y resuelve todo desde
/// ahí (ver <c>App.OnStartup</c>) -- de modo que estos servicios Scoped se comportan,
/// en la práctica, como si fueran Singleton durante la sesión, sin tener que tocar su
/// ciclo de vida ya definido en Fase 1/2. Por eso los tipos de esta capa también se
/// registran aquí como Scoped: así comparten exactamente el mismo scope raíz.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddSisLabTopoUi(this IServiceCollection services)
    {
        services.AddScoped<INavigationService, NavigationService>();

        // Ventanas raíz
        services.AddScoped<LoginView>();
        services.AddScoped<ShellView>();

        // ViewModels de ventana raíz
        services.AddScoped<LoginViewModel>();
        services.AddScoped<ShellViewModel>();

        // ViewModels de contenido del Shell. Fase 5a ya había reemplazado Equipos y
        // Préstamos; esta fase (5b) reemplaza los 3 restantes (Dashboard/Historial/
        // Configuración), que hasta ahora eran PlaceholderViewModel.
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EquiposViewModel>();
        services.AddTransient<PrestamosViewModel>();
        services.AddTransient<HistorialViewModel>();
        services.AddTransient<ConfiguracionViewModel>();

        // Diálogos de Fase 5 (ver comentario XML de IDialogService sobre el patrón
        // elegido): las ventanas se registran en DI para que DialogService pueda
        // resolverlas; sus ViewModels los construye directamente quien abre el
        // diálogo (nunca el contenedor), así que NO se registran aquí.
        services.AddTransient<Equipos.EquipoFormView>();
        services.AddTransient<Prestamos.NuevoPrestamoView>();
        services.AddTransient<Prestamos.EquipoSearchView>();
        services.AddTransient<Prestamos.DevolucionView>();
        services.AddTransient<Prestamos.ComprobantePreviewView>();
        services.AddTransient<Prestamos.DetallePrestamoView>();
        services.AddTransient<Dashboard.SeleccionarPrestamoActivoView>();

        services.AddSingleton<IDialogService>(sp => new DialogService(sp, new Dictionary<Type, Type>
        {
            [typeof(EquipoFormViewModel)] = typeof(Equipos.EquipoFormView),
            [typeof(NuevoPrestamoViewModel)] = typeof(Prestamos.NuevoPrestamoView),
            [typeof(EquipoSearchViewModel)] = typeof(Prestamos.EquipoSearchView),
            [typeof(DevolucionViewModel)] = typeof(Prestamos.DevolucionView),
            [typeof(ComprobantePreviewViewModel)] = typeof(Prestamos.ComprobantePreviewView),
            [typeof(DetallePrestamoViewModel)] = typeof(Prestamos.DetallePrestamoView),
            [typeof(SeleccionarPrestamoActivoViewModel)] = typeof(Dashboard.SeleccionarPrestamoActivoView),
        }));

#if DEBUG
        // Solo en builds DEBUG -- ver el comentario XML de DevPasswordSeeder.
        services.AddScoped<DevPasswordSeeder>();
#endif

        return services;
    }
}
