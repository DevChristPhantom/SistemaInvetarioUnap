using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Services;

/// <summary>
/// Servicio de negocio para equipos topográficos. Puerto 1:1 de
/// <c>service.EquipoService</c> (Java), con nombres async por convención idiomática de C#.
/// </summary>
public interface IEquipoService
{
    /// <summary>Todos los equipos registrados. Traga cualquier error de lectura y devuelve una lista vacía (igual que Java).</summary>
    Task<List<Equipo>> ObtenerTodosAsync(CancellationToken ct = default);

    /// <summary>Equipos actualmente disponibles (no prestados).</summary>
    Task<List<Equipo>> ObtenerDisponiblesAsync(CancellationToken ct = default);

    /// <summary>Búsqueda de subcadena, sin distinguir mayúsculas/minúsculas, sobre código/denominación/marca/modelo/serie.</summary>
    Task<List<Equipo>> BuscarPorTextoAsync(string? texto, CancellationToken ct = default);

    /// <summary>Filtra por estado exacto; si es <c>null</c> devuelve todos.</summary>
    Task<List<Equipo>> FiltrarPorEstadoAsync(EstadoEquipo? estado, CancellationToken ct = default);

    /// <summary>Filtra por tipo exacto; si es <c>null</c> devuelve todos.</summary>
    Task<List<Equipo>> FiltrarPorTipoAsync(TipoEquipo? tipo, CancellationToken ct = default);

    /// <summary>Busca un equipo por código. Traga cualquier error y devuelve <c>null</c> (igual que Java devuelve <c>Optional.empty()</c>).</summary>
    Task<Equipo?> BuscarPorCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Registra un equipo nuevo. Valida y exige unicidad de código.</summary>
    Task RegistrarAsync(Equipo equipo, CancellationToken ct = default);

    /// <summary>Actualiza un equipo existente. Valida y exige que el equipo ya exista.</summary>
    Task ActualizarAsync(Equipo equipo, CancellationToken ct = default);

    /// <summary>Elimina un equipo. Exige que exista y que esté disponible (no en préstamo activo).</summary>
    Task EliminarAsync(string codigo, CancellationToken ct = default);

    /// <summary>Conteo de equipos agrupados por estado.</summary>
    Task<Dictionary<EstadoEquipo, int>> ContarPorEstadoAsync(CancellationToken ct = default);

    /// <summary>Conteo de equipos agrupados por tipo.</summary>
    Task<Dictionary<TipoEquipo, int>> ContarPorTipoAsync(CancellationToken ct = default);

    /// <summary>
    /// Importa/actualiza equipos desde un archivo Excel externo (columnas 0-7: código,
    /// denominación, modelo, marca, serie, estado, tipo, observación). Los fallos por
    /// fila individual se registran y no abortan la importación completa.
    /// </summary>
    Task ImportarDesdeExcelAsync(string rutaArchivo, CancellationToken ct = default);
}
