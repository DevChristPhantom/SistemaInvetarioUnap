using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Repositories;

/// <inheritdoc cref="IEquipoRepository"/>
public class EquipoRepository : IEquipoRepository
{
    private readonly SisLabTopoDbContext _context;
    private readonly ILogger<EquipoRepository> _logger;

    public EquipoRepository(SisLabTopoDbContext context, ILogger<EquipoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Equipo>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.Equipos.AsNoTracking().OrderBy(e => e.Codigo).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer equipos de la base de datos.");
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al leer equipos de la base de datos.", ex);
        }
    }

    public async Task<Equipo?> BuscarPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var codigoNormalizado = codigo.Trim().ToLowerInvariant();
        try
        {
            return await _context.Equipos.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Codigo.ToLower() == codigoNormalizado, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar equipo por código '{Codigo}'.", codigo);
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al buscar el equipo en la base de datos.", ex);
        }
    }

    public async Task GuardarAsync(Equipo equipo, CancellationToken ct = default)
    {
        try
        {
            var codigoNormalizado = equipo.Codigo.Trim().ToLowerInvariant();
            var yaExiste = await _context.Equipos.AnyAsync(e => e.Codigo.ToLower() == codigoNormalizado, ct);
            if (yaExiste)
            {
                throw new ServiceException(ErrorCode.DatosInvalidos, "El código de equipo ya existe.");
            }

            _context.Equipos.Add(equipo);
            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar equipo '{Codigo}'.", equipo.Codigo);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al guardar el equipo en la base de datos.", ex);
        }
    }

    public async Task ActualizarAsync(Equipo equipo, CancellationToken ct = default)
    {
        try
        {
            var existente = await _context.Equipos.FirstOrDefaultAsync(e => e.Codigo == equipo.Codigo, ct);
            if (existente is null)
            {
                throw new ServiceException(ErrorCode.EquipoNoEncontrado, "El equipo a actualizar no existe.");
            }

            existente.Denominacion = equipo.Denominacion;
            existente.Modelo = equipo.Modelo;
            existente.Marca = equipo.Marca;
            existente.Serie = equipo.Serie;
            existente.Estado = equipo.Estado;
            existente.Tipo = equipo.Tipo;
            existente.Disponible = equipo.Disponible;
            existente.Observacion = equipo.Observacion;
            existente.FechaRegistro = equipo.FechaRegistro;

            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar equipo '{Codigo}'.", equipo.Codigo);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al actualizar el equipo en la base de datos.", ex);
        }
    }

    public async Task EliminarAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var existente = await _context.Equipos.FirstOrDefaultAsync(e => e.Codigo == codigo, ct);
            if (existente is null)
            {
                throw new ServiceException(ErrorCode.EquipoNoEncontrado, "El equipo a eliminar no existe.");
            }

            _context.Equipos.Remove(existente);
            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar equipo '{Codigo}'.", codigo);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al eliminar el equipo de la base de datos.", ex);
        }
    }

    public async Task ActualizarDisponibilidadAsync(string codigo, bool disponible, CancellationToken ct = default)
    {
        try
        {
            var existente = await _context.Equipos.FirstOrDefaultAsync(e => e.Codigo == codigo, ct);
            if (existente is null)
            {
                throw new ServiceException(ErrorCode.EquipoNoEncontrado, "Equipo no encontrado para actualizar disponibilidad.");
            }

            existente.Disponible = disponible;
            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar disponibilidad de equipo '{Codigo}'.", codigo);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al actualizar disponibilidad en la base de datos.", ex);
        }
    }
}
