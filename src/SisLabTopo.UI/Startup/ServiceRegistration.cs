using Microsoft.Extensions.DependencyInjection;
using SisLabTopo.UI.Configuracion;
using SisLabTopo.UI.Dashboard;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Equipos;
using SisLabTopo.UI.Historial;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.PasswordRecovery;
using SisLabTopo.UI.Prestamos;
using SisLabTopo.UI.Shell;
using SisLabTopo.UI.Theming;

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
/// ciclo de vida ya definido en Fase 1/2. <c>NavigationService</c> se registra Scoped
/// aquí por la misma razón (una única instancia para toda la sesión). Las ventanas raíz
/// (Login/Shell) y sus ViewModels, en cambio, se registran Transient -- ver el
/// comentario junto a su registro para la explicación (fix de un crash real encontrado
/// en la Fase 8 de QA).
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddSisLabTopoUi(this IServiceCollection services)
    {
        services.AddScoped<INavigationService, NavigationService>();

        // Rediseño visual: alterna en caliente entre Themes/Colors.xaml y
        // Themes/Colors.Dark.xaml (ver Theming/ThemeService). Singleton porque el tema
        // aplicado es un estado único de toda la sesión de la aplicación, igual que
        // IDialogService más abajo.
        services.AddSingleton<IThemeService, ThemeService>();

        // Ventanas raíz (Login/Shell): registradas Transient -- NO Scoped -- a propósito.
        //
        // Fix de la Fase 8 (QA final): originalmente estaban como Scoped, "para compartir
        // exactamente el mismo scope raíz" con los servicios Scoped de Data/Services (ver
        // nota de ciclo de vida más abajo). Eso funciona para servicios, pero un
        // Window de WPF no es un servicio corriente: una vez que se llama a Close() sobre
        // él (algo que ShowRootWindow hace deliberadamente con la ventana raíz anterior en
        // cada navegación) ese objeto Window queda inutilizable para siempre -- WPF lanza
        // InvalidOperationException ("no se puede llamar a Show... después de haberse
        // cerrado un elemento Window") si se lo vuelve a mostrar. Con Scoped + un único
        // scope de por vida de la app, el contenedor de DI devolvía la MISMA instancia de
        // LoginView/ShellView en cada NavigateTo, así que el primer tránsito Login->Shell
        // funcionaba (LoginView se cerraba por primera vez), pero el segundo tránsito
        // Shell->Login (es decir, "Cerrar sesión") pedía de nuevo esa misma instancia ya
        // cerrada de LoginView y la app moría con una excepción no controlada al invocar
        // Window.Show(). Se reprodujo de forma real durante el recorrido manual de la
        // Fase 8 (login -> Dashboard -> Cerrar sesión) y quedó confirmado en el Visor de
        // Eventos de Windows (.NET Runtime, "process was terminated due to an unhandled
        // exception"). LoginViewModel/ShellViewModel se cambian también a Transient (ver
        // más abajo) para evitar además que quede colgado cualquier estado obsoleto de la
        // sesión anterior (p.ej. un mensaje de error de login previo) en la nueva sesión.
        services.AddTransient<LoginView>();
        services.AddTransient<ShellView>();

        // Asistente de primer arranque (Fase 6): también es una ventana raíz (sustituye
        // a Login la primera vez que arranca la app, antes de que exista una contraseña
        // de administrador configurada), y por la misma razón que Login/Shell arriba se
        // registra Transient: solo se usa una vez por la vida de la base de datos, y no
        // queremos que la instancia (que llegó a tener en memoria el código de
        // recuperación en texto plano, aunque ya se limpia explícitamente al continuar)
        // quede cacheada indefinidamente por el resto de la sesión en el scope raíz de la
        // aplicación.
        services.AddTransient<FirstRunSetupView>();
        services.AddTransient<FirstRunSetupViewModel>();

        // ViewModels de ventana raíz: Transient por la misma razón que sus Views (ver
        // comentario arriba) -- ambos dependen únicamente de servicios Scoped/Singleton
        // que sí siguen viviendo en el único scope raíz de la app, así que no hay ningún
        // problema en resolver una instancia nueva de ViewModel en cada navegación.
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();

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

        // Diálogo de recuperación de contraseña (Fase 6): mismo patrón que los diálogos
        // de la Fase 5 (ver comentario XML de IDialogService) -- la ventana se registra
        // aquí para que DialogService pueda resolverla, pero su ViewModel lo construye
        // directamente LoginViewModel vía `new`, nunca el contenedor de DI.
        services.AddTransient<PasswordRecovery.PasswordRecoveryView>();

        services.AddSingleton<IDialogService>(sp => new DialogService(sp, new Dictionary<Type, Type>
        {
            [typeof(EquipoFormViewModel)] = typeof(Equipos.EquipoFormView),
            [typeof(NuevoPrestamoViewModel)] = typeof(Prestamos.NuevoPrestamoView),
            [typeof(EquipoSearchViewModel)] = typeof(Prestamos.EquipoSearchView),
            [typeof(DevolucionViewModel)] = typeof(Prestamos.DevolucionView),
            [typeof(ComprobantePreviewViewModel)] = typeof(Prestamos.ComprobantePreviewView),
            [typeof(DetallePrestamoViewModel)] = typeof(Prestamos.DetallePrestamoView),
            [typeof(SeleccionarPrestamoActivoViewModel)] = typeof(Dashboard.SeleccionarPrestamoActivoView),
            [typeof(PasswordRecoveryViewModel)] = typeof(PasswordRecovery.PasswordRecoveryView),
        }));

        return services;
    }
}
