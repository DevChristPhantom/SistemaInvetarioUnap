using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Repositories;

/// <summary>
/// Contrato para la persistencia y acceso a datos de préstamos e ítems de préstamo,
/// además de los parámetros de configuración clave/valor. Puerto 1:1 (mismos métodos)
/// de <c>repository.PrestamoRepository</c> (Java).
/// </summary>
public interface IPrestamoRepository
{
    /// <summary>Obtiene todos los préstamos registrados.</summary>
    Task<List<Prestamo>> ObtenerTodosAsync(CancellationToken ct = default);

    /// <summary>Busca un préstamo por su identificador único (UUID).</summary>
    Task<Prestamo?> BuscarPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Guarda un préstamo principal. Lanza <see cref="ServiceException"/> (DatosInvalidos) si el ID ya existe.</summary>
    Task GuardarAsync(Prestamo prestamo, CancellationToken ct = default);

    /// <summary>Actualiza un préstamo existente. Lanza <see cref="ServiceException"/> (PrestamoNoEncontrado) si no existe.</summary>
    Task ActualizarAsync(Prestamo prestamo, CancellationToken ct = default);

    /// <summary>Obtiene todos los detalles/ítems asociados a un préstamo específico.</summary>
    Task<List<DetallePrestamo>> ObtenerDetalleAsync(string prestamoId, CancellationToken ct = default);

    /// <summary>Guarda un detalle/ítem individual de préstamo.</summary>
    Task GuardarDetalleAsync(DetallePrestamo detalle, CancellationToken ct = default);

    /// <summary>Actualiza el estado (ej. si fue devuelto) de un detalle de préstamo.</summary>
    Task ActualizarDetalleAsync(DetallePrestamo detalle, CancellationToken ct = default);

    /// <summary>Obtiene todos los detalles de todos los préstamos.</summary>
    Task<List<DetallePrestamo>> ObtenerTodosDetallesAsync(CancellationToken ct = default);

    /// <summary>Lee un parámetro de configuración. Devuelve cadena vacía si no existe (paridad con Java).</summary>
    Task<string> ObtenerConfigAsync(string clave, CancellationToken ct = default);

    /// <summary>Guarda o actualiza (upsert) un parámetro de configuración.</summary>
    Task GuardarConfigAsync(string clave, string valor, CancellationToken ct = default);
}
