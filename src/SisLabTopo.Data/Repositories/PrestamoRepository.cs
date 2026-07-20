using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SisLabTopo.Data.Entities;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Repositories;

/// <inheritdoc cref="IPrestamoRepository"/>
public class PrestamoRepository : IPrestamoRepository
{
    private readonly SisLabTopoDbContext _context;
    private readonly ILogger<PrestamoRepository> _logger;

    public PrestamoRepository(SisLabTopoDbContext context, ILogger<PrestamoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Prestamo>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.Prestamos.AsNoTracking()
                .OrderByDescending(p => p.FechaRegistro)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer préstamos de la base de datos.");
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al leer préstamos.", ex);
        }
    }

    public async Task<Prestamo?> BuscarPorIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var idNormalizado = id.Trim().ToLowerInvariant();
        try
        {
            return await _context.Prestamos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id.ToLower() == idNormalizado, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar préstamo por id '{Id}'.", id);
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al buscar el préstamo en la base de datos.", ex);
        }
    }

    public async Task GuardarAsync(Prestamo prestamo, CancellationToken ct = default)
    {
        try
        {
            var idNormalizado = prestamo.Id.Trim().ToLowerInvariant();
            var yaExiste = await _context.Prestamos.AnyAsync(p => p.Id.ToLower() == idNormalizado, ct);
            if (yaExiste)
            {
                throw new ServiceException(ErrorCode.DatosInvalidos, "El ID de préstamo ya existe.");
            }

            _context.Prestamos.Add(prestamo);
            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar préstamo '{Id}'.", prestamo.Id);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al guardar el préstamo.", ex);
        }
    }

    public async Task ActualizarAsync(Prestamo prestamo, CancellationToken ct = default)
    {
        try
        {
            var existente = await _context.Prestamos.FirstOrDefaultAsync(p => p.Id == prestamo.Id, ct);
            if (existente is null)
            {
                throw new ServiceException(ErrorCode.PrestamoNoEncontrado, "El préstamo a actualizar no existe.");
            }

            existente.Docente = prestamo.Docente;
            existente.Curso = prestamo.Curso;
            existente.Semestre = prestamo.Semestre;
            existente.NombreEstudiante = prestamo.NombreEstudiante;
            existente.CodigoEstudiante = prestamo.CodigoEstudiante;
            existente.FechaPrestamo = prestamo.FechaPrestamo;
            existente.FechaDevolucion = prestamo.FechaDevolucion;
            existente.Estado = prestamo.Estado;
            existente.Observaciones = prestamo.Observaciones;
            existente.FechaRegistro = prestamo.FechaRegistro;

            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar préstamo '{Id}'.", prestamo.Id);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al actualizar el préstamo.", ex);
        }
    }

    public async Task<List<DetallePrestamo>> ObtenerDetalleAsync(string prestamoId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prestamoId))
        {
            return new List<DetallePrestamo>();
        }

        var idNormalizado = prestamoId.Trim().ToLowerInvariant();
        try
        {
            return await _context.DetallesPrestamo.AsNoTracking()
                .Where(d => d.PrestamoId.ToLower() == idNormalizado)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer detalle del préstamo '{PrestamoId}'.", prestamoId);
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al leer el detalle del préstamo.", ex);
        }
    }

    public async Task GuardarDetalleAsync(DetallePrestamo detalle, CancellationToken ct = default)
    {
        try
        {
            var idNormalizado = detalle.Id.Trim().ToLowerInvariant();
            var yaExiste = await _context.DetallesPrestamo.AnyAsync(d => d.Id.ToLower() == idNormalizado, ct);
            if (yaExiste)
            {
                throw new ServiceException(ErrorCode.DatosInvalidos, "El ID de detalle de préstamo ya existe.");
            }

            _context.DetallesPrestamo.Add(detalle);
            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar detalle de préstamo '{Id}'.", detalle.Id);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al guardar el detalle de préstamo.", ex);
        }
    }

    public async Task ActualizarDetalleAsync(DetallePrestamo detalle, CancellationToken ct = default)
    {
        try
        {
            var existente = await _context.DetallesPrestamo.FirstOrDefaultAsync(d => d.Id == detalle.Id, ct);
            if (existente is null)
            {
                throw new ServiceException(ErrorCode.PrestamoNoEncontrado, "El detalle de préstamo a actualizar no existe.");
            }

            existente.PrestamoId = detalle.PrestamoId;
            existente.EquipoCodigo = detalle.EquipoCodigo;
            existente.ObservacionItem = detalle.ObservacionItem;
            existente.Devuelto = detalle.Devuelto;

            await _context.SaveChangesAsync(ct);
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar detalle de préstamo '{Id}'.", detalle.Id);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al guardar el detalle de préstamo.", ex);
        }
    }

    public async Task<List<DetallePrestamo>> ObtenerTodosDetallesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.DetallesPrestamo.AsNoTracking().ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer todos los detalles de préstamo.");
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al leer detalles globales.", ex);
        }
    }

    public async Task<string> ObtenerConfigAsync(string clave, CancellationToken ct = default)
    {
        try
        {
            var claveNormalizada = clave.Trim().ToLowerInvariant();
            var entry = await _context.ConfigEntries.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Clave.ToLower() == claveNormalizada, ct);
            return entry?.Valor ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer parámetro de configuración '{Clave}'.", clave);
            throw new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "Error al leer parámetro de configuración.", ex);
        }
    }

    public async Task GuardarConfigAsync(string clave, string valor, CancellationToken ct = default)
    {
        try
        {
            var claveNormalizada = clave.Trim().ToLowerInvariant();
            var existente = await _context.ConfigEntries
                .FirstOrDefaultAsync(c => c.Clave.ToLower() == claveNormalizada, ct);

            if (existente is null)
            {
                _context.ConfigEntries.Add(new ConfigEntry { Clave = clave, Valor = valor });
            }
            else
            {
                existente.Valor = valor;
            }

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar parámetro de configuración '{Clave}'.", clave);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Error al guardar parámetro de configuración.", ex);
        }
    }
}
