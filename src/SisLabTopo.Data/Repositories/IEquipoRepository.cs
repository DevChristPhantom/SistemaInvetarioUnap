using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Repositories;

/// <summary>
/// Contrato para la persistencia y acceso a datos de equipos.
/// Puerto 1:1 (mismos métodos) de <c>repository.EquipoRepository</c> (Java), con
/// nombres async por convención idiomática de EF Core / C#.
/// </summary>
public interface IEquipoRepository
{
    /// <summary>Obtiene todos los equipos registrados.</summary>
    Task<List<Equipo>> ObtenerTodosAsync(CancellationToken ct = default);

    /// <summary>Busca un equipo por su código patrimonial único.</summary>
    Task<Equipo?> BuscarPorCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Guarda un nuevo equipo. Lanza <see cref="ServiceException"/> (DatosInvalidos) si el código ya existe.</summary>
    Task GuardarAsync(Equipo equipo, CancellationToken ct = default);

    /// <summary>Actualiza los datos de un equipo existente. Lanza <see cref="ServiceException"/> (EquipoNoEncontrado) si no existe.</summary>
    Task ActualizarAsync(Equipo equipo, CancellationToken ct = default);

    /// <summary>Elimina un equipo por su código. Lanza <see cref="ServiceException"/> (EquipoNoEncontrado) si no existe.</summary>
    Task EliminarAsync(string codigo, CancellationToken ct = default);

    /// <summary>Actualiza únicamente la disponibilidad de un equipo específico.</summary>
    Task ActualizarDisponibilidadAsync(string codigo, bool disponible, CancellationToken ct = default);
}
