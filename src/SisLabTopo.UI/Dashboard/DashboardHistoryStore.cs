using System.IO;
using System.Text.Json;

namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// Una "foto" de los 4 números de la fila 1 del Dashboard en un momento dado. Se usa
/// exclusivamente para calcular la comparativa/tendencia y el sparkline de
/// <see cref="Controls.StatCard"/> -- ver comentario de <see cref="IDashboardHistoryStore"/>.
/// </summary>
public sealed record DashboardSnapshot(
    DateTime Fecha,
    int TotalEquipos,
    int EquiposDisponibles,
    int PrestamosActivos,
    int DevolucionesHoy);

/// <summary>
/// Persistencia (solo de UI, nunca de dominio/negocio) de un historial corto de
/// "fotos" del Dashboard, para poder mostrar comparativa/tendencia + sparkline en las
/// tarjetas de estadística sin necesitar ningún endpoint nuevo de
/// <c>SisLabTopo.Services</c> (el encargo de esta fase es explícito: la paginación y
/// cualquier lógica nueva debe quedarse del lado de la UI). <see cref="Equipo"/>/
/// <see cref="Prestamo"/> no tienen (ni deben tener) una serie temporal de inventario
/// por día -- por eso se aproxima guardando, en un archivo JSON local, el resultado de
/// cada carga real del Dashboard (se sobrescribe/crece con cada <c>F5</c>/navegación),
/// y comparando contra la foto anterior. Es una aproximación deliberada, documentada:
/// la "tendencia" de estas 3 tarjetas (Total Equipos/Disponibles/Devoluciones Hoy) es
/// "vs. la última vez que se cargó el Dashboard", no necesariamente "vs. ayer" -- se
/// rotula así explícitamente en <c>DashboardViewModel</c> para no insinuar una precisión
/// que no existe. La tarjeta de "Préstamos Activos" en cambio SÍ tiene datos mensuales
/// reales ya disponibles (<c>PrestamosPorMesAsync</c>) y los usa en su lugar.
/// </summary>
public interface IDashboardHistoryStore
{
    /// <summary>Última foto guardada antes de la carga actual (null en el primer arranque).</summary>
    DashboardSnapshot? ObtenerAnterior();

    /// <summary>Hasta <paramref name="maxPuntos"/> fotos más recientes, en orden cronológico ascendente (para alimentar un sparkline).</summary>
    IReadOnlyList<DashboardSnapshot> ObtenerSerie(int maxPuntos);

    /// <summary>Agrega una nueva foto (recorta el historial a un máximo razonable de puntos guardados).</summary>
    void Guardar(DashboardSnapshot snapshot);
}

/// <inheritdoc cref="IDashboardHistoryStore"/>
public sealed class DashboardHistoryStore : IDashboardHistoryStore
{
    private const int MaxPuntosGuardados = 30;

    private readonly string _rutaArchivo;

    public DashboardHistoryStore(string? rutaArchivo = null)
    {
        _rutaArchivo = rutaArchivo ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SisLabTopo", "dashboard_history.json");
    }

    public DashboardSnapshot? ObtenerAnterior() => CargarTodo().LastOrDefault();

    public IReadOnlyList<DashboardSnapshot> ObtenerSerie(int maxPuntos) =>
        CargarTodo().TakeLast(Math.Max(1, maxPuntos)).ToList();

    public void Guardar(DashboardSnapshot snapshot)
    {
        try
        {
            var historial = CargarTodo().ToList();
            historial.Add(snapshot);
            if (historial.Count > MaxPuntosGuardados)
            {
                historial = historial.TakeLast(MaxPuntosGuardados).ToList();
            }

            var directorio = Path.GetDirectoryName(_rutaArchivo);
            if (!string.IsNullOrEmpty(directorio))
            {
                Directory.CreateDirectory(directorio);
            }

            File.WriteAllText(_rutaArchivo, JsonSerializer.Serialize(historial));
        }
        catch (IOException)
        {
            // El historial de tendencia es una mejora visual, no una funcionalidad
            // crítica: si el disco no es escribible (permisos, disco lleno, etc.) el
            // Dashboard simplemente no muestra tendencia en la próxima carga -- nunca
            // debe tumbar la pantalla por esto.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private List<DashboardSnapshot> CargarTodo()
    {
        try
        {
            if (!File.Exists(_rutaArchivo))
            {
                return new List<DashboardSnapshot>();
            }

            var json = File.ReadAllText(_rutaArchivo);
            return JsonSerializer.Deserialize<List<DashboardSnapshot>>(json) ?? new List<DashboardSnapshot>();
        }
        catch (IOException)
        {
            return new List<DashboardSnapshot>();
        }
        catch (UnauthorizedAccessException)
        {
            return new List<DashboardSnapshot>();
        }
        catch (JsonException)
        {
            return new List<DashboardSnapshot>();
        }
    }
}
