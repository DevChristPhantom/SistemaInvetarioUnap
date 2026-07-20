using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SisLabTopo.Data;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.Startup;

namespace SisLabTopo.UI;

/// <summary>
/// Punto de arranque de la aplicación. Reemplaza el arranque WPF por defecto
/// (<c>StartupUri</c>) por un host de <c>Microsoft.Extensions.Hosting</c>
/// (<see cref="HostApplicationBuilder"/>), equivalente moderno del localizador de
/// servicios manual <c>AppContext.java</c> + <c>logback.xml</c> de la versión Java:
/// aquí se cablean todas las capas (Data/Services/Reports/UI), se configura Serilog con
/// archivo rotativo diario, se aseguran las migraciones de la base de datos SQLite, y
/// solo entonces se muestra la primera ventana (Login) -- replicando el flujo Java de
/// "la app siempre arranca en Login, nunca directo al Shell".
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private IServiceScope? _rootScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Fuerza el pipeline de renderizado de WPF (Milcore/Direct3D) a modo software.
        // Diagnóstico: en máquinas/VMs sin aceleración 3D confiable (o con el driver de
        // GPU deshabilitado/virtualizado -- el caso de esta estación de desarrollo), el
        // "tier" de render por hardware que WPF detecta al arrancar queda en un estado en
        // el que el marco de la ventana (dibujado por DWM, fuera de WPF) se ve
        // perfectamente, pero la superficie de composición de WPF (todo lo que SÍ dibuja
        // WPF: Grid, Border, TextBlock, etc.) se presenta en blanco -- sin ninguna
        // excepción, porque no es un error de binding/plantilla: Loaded, los bindings y
        // el DispatcherTimer del lockout siguen corriendo con total normalidad, ya que el
        // árbol visual SÍ existe, solo no se pinta. Establecer ProcessRenderMode a
        // SoftwareOnly (antes de crear cualquier ventana) obliga a WPF a componer con el
        // rasterizador por software en vez de Direct3D, lo cual resuelve este problema a
        // costa de un pequeño impacto de rendimiento, aceptable para una app de
        // formularios como esta.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        // Inicialización requerida por LiveCharts2 (Dashboard, Fase 5b) antes de crear
        // cualquier control de gráfico: registra el "motor de dibujo" SkiaSharp (los
        // controles WPF de LiveChartsCore.SkiaSharpView.WPF renderizan sobre un
        // WriteableBitmap propio vía SkiaSharp, no sobre el pipeline Direct3D de WPF, así
        // que conviven sin problema con el RenderMode.SoftwareOnly forzado arriba) y los
        // mapeadores por defecto (tipos numéricos simples -> punto de datos).
        LiveCharts.Configure(config => config.AddSkiaSharp().AddDefaultMappers());

        ConfigurarSerilog();

        try
        {
            var builder = Host.CreateApplicationBuilder();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(dispose: true);

            builder.Services.AddSisLabTopoData();
            builder.Services.AddSisLabTopoServices();
            builder.Services.AddSisLabTopoReports();
            builder.Services.AddSisLabTopoUi();

            _host = builder.Build();
            await _host.StartAsync();

            // Un único scope para toda la vida de la aplicación (ver comentario de
            // ServiceRegistration.AddSisLabTopoUi sobre por qué): todos los servicios
            // Scoped de Data/Services (DbContext incluido) se resuelven a partir de aquí.
            _rootScope = _host.Services.CreateScope();
            var services = _rootScope.ServiceProvider;

            var initializer = services.GetRequiredService<DatabaseInitializer>();
            await initializer.InicializarSiNoExisteAsync();

#if DEBUG
            var seeder = services.GetRequiredService<DevPasswordSeeder>();
            await seeder.SembrarSiHaceFaltaAsync();
#endif

            var navigationService = services.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<LoginViewModel>();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error no controlado durante el arranque de la aplicación.");
            MessageBox.Show(
                $"No se pudo iniciar SisLab-Topo:\n\n{ex.Message}",
                "Error de arranque",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _rootScope?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// Configura Serilog con archivo rotativo diario en
    /// <c>%LOCALAPPDATA%\SisLabTopo\logs\sislabtopo-.log</c> (patrón de nombre con
    /// fecha, retención 30 días) -- equivalente moderno de
    /// <c>src/main/resources/logback.xml</c> (Java), que rotaba diariamente con
    /// <c>maxHistory=30</c> hacia <c>~/.gemini/antigravity/sislab-topo.&lt;fecha&gt;.log</c>.
    /// Se añade también un sink de consola/Debug para poder ver la actividad durante el
    /// desarrollo.
    /// </summary>
    private static void ConfigurarSerilog()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SisLabTopo", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "sislabtopo-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
