using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using SisLabTopo.UI.Converters;
using SisLabTopo.UI.Login;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImaging = System.Drawing.Imaging;
using DrawingRectangle = System.Drawing.Rectangle;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Smoke test de renderizado real (no solo "el ViewModel se comporta bien"): muestra
/// <see cref="LoginView"/> de verdad y captura los píxeles reales de su HWND vía
/// <c>user32!PrintWindow</c>, verificando que el contenido no sea un lienzo uniforme
/// (blanco u otro color sólido) -- es decir, que haya controles/texto/branding
/// realmente pintados, no solo un árbol visual construido en memoria.
///
/// <para>
/// <b>Contexto y una limitación honesta encontrada al escribir esta prueba:</b> el
/// 2026-07-20 se encontró manualmente (con una captura de pantalla completa del
/// proceso empaquetado real, <c>dotnet run --project src/SisLabTopo.UI</c>) que la
/// ventana de Login abría con título correcto y sin ninguna excepción en el log
/// (bindings, <see cref="LoginViewModel"/> y el <c>DispatcherTimer</c> del lockout
/// funcionando con total normalidad), pero el área de contenido se veía completamente
/// en blanco -- un problema del tier de renderizado por hardware de WPF en esta
/// máquina/entorno, corregido en <c>App.xaml.cs</c> con
/// <c>RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly</c>.
/// </para>
/// <para>
/// Se intentó (y se descartó) que ESTA prueba automatizada protegiera específicamente
/// contra ESE bug puntual: se comprobó empíricamente que, corriendo dentro del proceso
/// host de <c>dotnet test</c>, tanto <c>RenderTargetBitmap</c> como
/// <c>PrintWindow</c> sobre el HWND real producen contenido visual correcto incluso
/// con la línea de fix comentada en <c>App.xaml.cs</c> -- el proceso de pruebas no
/// reproduce las condiciones de tier de hardware que sí afectaban al proceso empaquetado
/// real. Por lo tanto: <b>esta prueba SÍ protege contra regresiones de plantilla/
/// binding que dejen la ventana sin contenido</b> (un `DataTemplate` no resuelto, un
/// `Visibility` mal atado, etc.), pero NO contra una futura eliminación accidental de
/// `RenderOptions.ProcessRenderMode` en `App.xaml.cs` -- esa clase de bug específica
/// solo se detectó, y solo se puede volver a detectar de forma confiable, con
/// verificación visual manual (captura de pantalla) del ejecutable empaquetado real.
/// Queda documentado aquí como pendiente para cuando exista un pipeline de CI real
/// (Fase 7/8) que corra sobre una máquina con las mismas condiciones que producción.
/// </para>
/// </summary>
public class LoginViewRenderingTests
{
    private const int PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [Fact]
    public void LoginView_AlMostrarseEnPantalla_ProduceContenidoVisualNoUniforme()
    {
        var estadisticas = EjecutarEnHiloSta(() =>
        {
            AsegurarRecursosDeAplicacion();

            var view = new LoginView
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -6000, // fuera del área visible del monitor: PrintWindow no lo necesita en pantalla
                Top = -6000,
                ShowInTaskbar = false,
                Topmost = true,
            };

            view.Show();

            // Deja que WPF procese Loaded/layout/composición on-screen antes de capturar.
            for (var i = 0; i < 15; i++)
            {
                PumpDispatcherFrame();
            }

            var hwnd = new WindowInteropHelper(view).Handle;
            var ancho = Math.Max(1, (int)view.ActualWidth);
            var alto = Math.Max(1, (int)view.ActualHeight);

            var stats = CapturarEstadisticasDeVentanaReal(hwnd, ancho, alto);

            view.Close();

            return stats;
        });

        Assert.True(estadisticas.PixelesUnicos > 5,
            $"PrintWindow sobre la ventana real de LoginView produjo solo {estadisticas.PixelesUnicos} " +
            "color(es) único(s) -- indicio de un lienzo en blanco/uniforme (regresión del bug de " +
            "renderizado de 2026-07-20: RenderOptions.ProcessRenderMode en App.OnStartup), no de un " +
            "formulario con controles reales.");

        Assert.True(estadisticas.DesviacionEstandar > 1.0,
            $"La desviación estándar de los píxeles capturados de la ventana real fue " +
            $"{estadisticas.DesviacionEstandar:F2}, demasiado baja para un formulario con texto, campos " +
            "y branding institucional -- sugiere una superficie prácticamente uniforme (posible " +
            "regresión del bug de ventana en blanco).");
    }

    private static PixelStats CapturarEstadisticasDeVentanaReal(IntPtr hwnd, int ancho, int alto)
    {
        using var bitmap = new DrawingBitmap(ancho, alto);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
            {
                throw new InvalidOperationException(
                    $"PrintWindow falló (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        var datos = bitmap.LockBits(
            new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height),
            DrawingImaging.ImageLockMode.ReadOnly,
            DrawingImaging.PixelFormat.Format32bppArgb);
        try
        {
            var totalBytes = datos.Stride * bitmap.Height;
            var bytes = new byte[totalBytes];
            Marshal.Copy(datos.Scan0, bytes, 0, totalBytes);

            var colores = new HashSet<int>();
            double suma = 0;
            double sumaCuadrados = 0;
            var totalPixeles = bitmap.Width * bitmap.Height;

            for (var fila = 0; fila < bitmap.Height; fila++)
            {
                var offsetFila = fila * datos.Stride;
                for (var col = 0; col < bitmap.Width; col++)
                {
                    var i = offsetFila + col * 4;
                    var valor = (bytes[i] + bytes[i + 1] + bytes[i + 2]) / 3.0;
                    suma += valor;
                    sumaCuadrados += valor * valor;
                    colores.Add((bytes[i] << 16) | (bytes[i + 1] << 8) | bytes[i + 2]);
                }
            }

            var media = suma / totalPixeles;
            var varianza = (sumaCuadrados / totalPixeles) - (media * media);

            return new PixelStats(colores.Count, Math.Sqrt(Math.Max(0, varianza)));
        }
        finally
        {
            bitmap.UnlockBits(datos);
        }
    }

    /// <summary>
    /// Fuera de <c>App.OnStartup</c> no existe una <see cref="Application"/> real, así
    /// que <c>StaticResource</c> hacia los diccionarios de tema (Colors/Typography/
    /// Spacing/Controls, fusionados normalmente en <c>App.xaml</c>) no resolvería nada.
    /// Se replica aquí el mismo <c>MergedDictionaries</c> que declara <c>App.xaml</c>
    /// para que <see cref="LoginView"/> se construya en las mismas condiciones que en
    /// producción -- pero deliberadamente SIN llamar a <c>App.OnStartup</c> (eso
    /// arrancaría el host de DI real, la migración de base de datos, etc., acoplando
    /// esta prueba de renderizado a infraestructura que no le corresponde probar), y
    /// SIN establecer <c>RenderOptions.ProcessRenderMode</c> aquí -- esa línea vive
    /// únicamente en <c>App.xaml.cs</c> y es exactamente lo que esta prueba vigila.
    /// </summary>
    /// <summary>
    /// <c>internal</c> (no <c>private</c>) a propósito: <see cref="SearchableDataGridPaginationTests"/>
    /// reutiliza este mismo arnés de Application/MergedDictionaries en vez de duplicarlo,
    /// ya que ambas pruebas necesitan exactamente el mismo entorno WPF mínimo fuera de
    /// <c>App.OnStartup</c>.
    /// </summary>
    internal static void AsegurarRecursosDeAplicacion()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        var rutasTemas = new[]
        {
            "Themes/Colors.xaml",
            "Themes/Typography.xaml",
            "Themes/Spacing.xaml",
            "Themes/Controls.xaml",
        };

        foreach (var ruta in rutasTemas)
        {
            var recurso = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/SisLabTopo.UI;component/{ruta}", UriKind.Absolute),
            };
            app.Resources.MergedDictionaries.Add(recurso);
        }

        // Réplica de los recursos "sueltos" (no fusionados) que App.xaml declara junto
        // a los MergedDictionaries -- los convertidores que las vistas referencian por
        // StaticResource.
        app.Resources.Add("BoolToVisibilityConverter", new BooleanToVisibilityConverter());
        app.Resources.Add("InverseBoolConverter", new InverseBoolConverter());
        app.Resources.Add("StringToVisibilityConverter", new StringToVisibilityConverter());
        app.Resources.Add("ViewModelIsTypeConverter", new ViewModelIsTypeConverter());
        app.Resources.Add("EstadoEquipoToBrushConverter", new EstadoEquipoToBrushConverter());
        app.Resources.Add("EstadoEquipoToTextConverter", new EstadoEquipoToTextConverter());
        app.Resources.Add("EstadoPrestamoToBrushConverter", new EstadoPrestamoToBrushConverter());
        app.Resources.Add("EstadoPrestamoToTextConverter", new EstadoPrestamoToTextConverter());
        app.Resources.Add("DisponibilidadToBrushConverter", new DisponibilidadToBrushConverter());
        app.Resources.Add("DisponibilidadToTextConverter", new DisponibilidadToTextConverter());
    }

    private static void PumpDispatcherFrame()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// WPF (Window/Dispatcher) y PrintWindow exigen un hilo con mensajes/apartamento
    /// STA. xUnit ejecuta las pruebas en hilos del pool (MTA), así que el cuerpo se
    /// corre en un hilo STA dedicado y se espera su resultado.
    /// </summary>
    /// <summary><c>internal</c> por el mismo motivo que <see cref="AsegurarRecursosDeAplicacion"/>: reutilizado por <see cref="SearchableDataGridPaginationTests"/>.</summary>
    internal static T EjecutarEnHiloSta<T>(Func<T> accion)
    {
        T resultado = default!;
        Exception? excepcion = null;

        var hilo = new Thread(() =>
        {
            try
            {
                resultado = accion();
            }
            catch (Exception ex)
            {
                excepcion = ex;
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.IsBackground = true;
        hilo.Start();
        hilo.Join();

        if (excepcion is not null)
        {
            throw excepcion;
        }

        return resultado;
    }

    private readonly record struct PixelStats(int PixelesUnicos, double DesviacionEstandar);
}
