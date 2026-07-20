using SisLabTopo.Domain.Models;

namespace SisLabTopo.Services;

/// <summary>Cantidad de veces que un equipo fue prestado. Resultado ordenado de <see cref="IPrestamoService.EquiposMasPrestadosAsync"/>.</summary>
public record EquipoConteo(string CodigoEquipo, int Cantidad);

/// <summary>Cantidad de préstamos registrados en un mes calendario concreto. Resultado ordenado (más antiguo primero) de <see cref="IPrestamoService.PrestamosPorMesAsync"/>.</summary>
public record PrestamosPorMesItem(int Anio, int Mes, int Cantidad);

/// <summary>
/// Servicio de negocio (núcleo transaccional) para préstamos y devoluciones de
/// equipos. Puerto 1:1 de <c>service.PrestamoService</c> (Java), con nombres async por
/// convención idiomática de C#.
///
/// Mejora deliberada respecto a Java (documentada en el plan de migración, Fase 2):
/// <see cref="RegistrarPrestamoAsync"/> y <see cref="RegistrarDevolucionAsync"/> usan
/// una transacción real de EF Core/SQLite en vez del "rollback manual, deshacer uno por
/// uno" que hacía la versión Java por las limitaciones del motor Excel.
/// </summary>
public interface IPrestamoService
{
    /// <summary>Todos los préstamos registrados. Traga cualquier error de lectura y devuelve una lista vacía (igual que Java).</summary>
    Task<List<Prestamo>> ObtenerTodosAsync(CancellationToken ct = default);

    /// <summary>Préstamos con estado Activo o Vencido.</summary>
    Task<List<Prestamo>> ObtenerActivosAsync(CancellationToken ct = default);

    /// <summary>Busca un préstamo por ID. Traga cualquier error y devuelve <c>null</c>.</summary>
    Task<Prestamo?> BuscarPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Detalles/ítems (equipos individuales) de un préstamo. Traga cualquier error y devuelve una lista vacía.</summary>
    Task<List<DetallePrestamo>> ObtenerDetalleAsync(string prestamoId, CancellationToken ct = default);

    /// <summary>
    /// Registra un préstamo nuevo con uno o más equipos. Valida <paramref name="prestamo"/>,
    /// exige al menos un código de equipo, y pre-valida que TODOS los códigos existan y
    /// estén disponibles antes de escribir nada. Devuelve el ID del préstamo creado.
    /// </summary>
    Task<string> RegistrarPrestamoAsync(Prestamo prestamo, IReadOnlyList<string> codigosEquipos, CancellationToken ct = default);

    /// <summary>
    /// Registra la devolución de un préstamo. Exige que exista y que no esté ya
    /// devuelto. Libera todos los equipos asociados y concatena
    /// <paramref name="observaciones"/> (si no está vacío) al final de las
    /// observaciones existentes.
    /// </summary>
    Task RegistrarDevolucionAsync(string prestamoId, string? observaciones, CancellationToken ct = default);

    /// <summary>Búsqueda de subcadena, sin distinguir mayúsculas/minúsculas, sobre estudiante/código de estudiante/docente.</summary>
    Task<List<Prestamo>> BuscarPorEstudianteAsync(string? textoBusqueda, CancellationToken ct = default);

    /// <summary>Filtra por semestre exacto (sin distinguir mayúsculas/minúsculas); si está vacío devuelve todos.</summary>
    Task<List<Prestamo>> FiltrarPorSemestreAsync(string? semestre, CancellationToken ct = default);

    /// <summary>Filtra por rango de fechas de préstamo (inclusive); si alguno de los extremos es <c>null</c> devuelve todos.</summary>
    Task<List<Prestamo>> FiltrarPorRangoFechasAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);

    /// <summary>Los <paramref name="top"/> equipos con más préstamos históricos, en orden descendente.</summary>
    Task<List<EquipoConteo>> EquiposMasPrestadosAsync(int top, CancellationToken ct = default);

    /// <summary>
    /// Cantidad de préstamos por mes calendario de los últimos <paramref name="meses"/>
    /// meses (incluyendo el actual), de más antiguo a más reciente, rellenando con cero
    /// los meses sin datos.
    /// </summary>
    Task<List<PrestamosPorMesItem>> PrestamosPorMesAsync(int meses, CancellationToken ct = default);

    /// <summary>Cantidad de préstamos con estado Activo o Vencido.</summary>
    Task<long> ContarActivosAsync(CancellationToken ct = default);

    /// <summary>Cantidad de préstamos devueltos en el día de hoy (fecha local del sistema).</summary>
    Task<long> ContarDevueltosHoyAsync(CancellationToken ct = default);
}
