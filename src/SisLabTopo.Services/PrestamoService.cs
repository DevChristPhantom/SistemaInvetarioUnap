using FluentValidation;
using Microsoft.Extensions.Logging;
using SisLabTopo.Data;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services;

/// <inheritdoc cref="IPrestamoService"/>
public class PrestamoService : IPrestamoService
{
    private readonly IPrestamoRepository _prestamoRepo;
    private readonly IEquipoRepository _equipoRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<Prestamo> _validator;
    private readonly ILogger<PrestamoService> _logger;

    public PrestamoService(
        IPrestamoRepository prestamoRepo,
        IEquipoRepository equipoRepo,
        IUnitOfWork unitOfWork,
        IValidator<Prestamo> validator,
        ILogger<PrestamoService> logger)
    {
        _prestamoRepo = prestamoRepo;
        _equipoRepo = equipoRepo;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _logger = logger;
    }

    public async Task<List<Prestamo>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _prestamoRepo.ObtenerTodosAsync(ct);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Error al obtener préstamos.");
            return new List<Prestamo>();
        }
    }

    public async Task<List<Prestamo>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido).ToList();
    }

    public async Task<Prestamo?> BuscarPorIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            return await _prestamoRepo.BuscarPorIdAsync(id, ct);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Error al buscar préstamo por ID: {Id}", id);
            return null;
        }
    }

    public async Task<List<DetallePrestamo>> ObtenerDetalleAsync(string prestamoId, CancellationToken ct = default)
    {
        try
        {
            return await _prestamoRepo.ObtenerDetalleAsync(prestamoId, ct);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Error al obtener detalle de préstamo: {PrestamoId}", prestamoId);
            return new List<DetallePrestamo>();
        }
    }

    public async Task<string> RegistrarPrestamoAsync(Prestamo prestamo, IReadOnlyList<string> codigosEquipos, CancellationToken ct = default)
    {
        ValidationHelper.Validar(_validator, prestamo);
        if (codigosEquipos is null || codigosEquipos.Count == 0)
        {
            throw new ServiceException(ErrorCode.DatosInvalidos, "Debe seleccionar al menos un equipo para realizar el préstamo.");
        }

        // 1. Pre-validar existencia y disponibilidad de TODOS los equipos antes de escribir nada.
        var equiposAEditar = new List<Equipo>();
        foreach (var codigo in codigosEquipos)
        {
            var equipo = await _equipoRepo.BuscarPorCodigoAsync(codigo, ct);
            if (equipo is null)
            {
                throw new ServiceException(ErrorCode.EquipoNoEncontrado, $"El equipo con código {codigo} no existe.");
            }

            if (!equipo.Disponible)
            {
                throw new ServiceException(ErrorCode.EquipoNoDisponible, $"El equipo con código {codigo} no está disponible.");
            }

            equiposAEditar.Add(equipo);
        }

        // 2. Transacción real de EF Core (mejora respecto a Java: reemplaza el rollback
        // manual "deshacer uno por uno" que hacía la versión Java por las limitaciones
        // del motor Excel, que no soportaba transacciones atómicas reales).
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var equipo in equiposAEditar)
            {
                await _equipoRepo.ActualizarDisponibilidadAsync(equipo.Codigo, false, ct);
            }

            await _prestamoRepo.GuardarAsync(prestamo, ct);

            foreach (var equipo in equiposAEditar)
            {
                var detalle = new DetallePrestamo
                {
                    Id = Guid.NewGuid().ToString(),
                    PrestamoId = prestamo.Id,
                    EquipoCodigo = equipo.Codigo,
                    ObservacionItem = "Estado al entregar: " + equipo.Estado.Etiqueta(),
                    Devuelto = false
                };
                await _prestamoRepo.GuardarDetalleAsync(detalle, ct);
            }

            await transaction.CommitAsync(ct);
            return prestamo.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar préstamo. Revirtiendo transacción...");
            await transaction.RollbackAsync(ct);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Fallo al registrar la transacción de préstamo en la base de datos.", ex);
        }
    }

    public async Task RegistrarDevolucionAsync(string prestamoId, string? observaciones, CancellationToken ct = default)
    {
        var prestamo = await _prestamoRepo.BuscarPorIdAsync(prestamoId, ct);
        if (prestamo is null)
        {
            throw new ServiceException(ErrorCode.PrestamoNoEncontrado, "El préstamo no existe.");
        }

        if (prestamo.Estado == EstadoPrestamo.Devuelto)
        {
            throw new ServiceException(ErrorCode.PrestamoYaDevuelto, "El préstamo ya fue devuelto con anterioridad.");
        }

        prestamo.Estado = EstadoPrestamo.Devuelto;
        prestamo.FechaDevolucion = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(observaciones))
        {
            var obsActuales = prestamo.Observaciones ?? string.Empty;
            prestamo.Observaciones = obsActuales + " | Devolución: " + observaciones.Trim();
        }

        var detalles = await _prestamoRepo.ObtenerDetalleAsync(prestamoId, ct);

        // Transacción real de EF Core: Java no podía hacer esto atómico por las
        // limitaciones del motor Excel; con SQLite/EF no hay excusa para dejar esta
        // operación (varios equipos + préstamo) sin atomicidad real.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var detalle in detalles)
            {
                detalle.Devuelto = true;
                await _prestamoRepo.ActualizarDetalleAsync(detalle, ct);
                await _equipoRepo.ActualizarDisponibilidadAsync(detalle.EquipoCodigo, true, ct);
            }

            await _prestamoRepo.ActualizarAsync(prestamo, ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar la devolución. Revirtiendo transacción...");
            await transaction.RollbackAsync(ct);
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Fallo al registrar la devolución en la base de datos.", ex);
        }
    }

    public async Task<List<Prestamo>> BuscarPorEstudianteAsync(string? textoBusqueda, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(textoBusqueda))
        {
            return await ObtenerTodosAsync(ct);
        }

        var query = textoBusqueda.Trim();
        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(p =>
                Contiene(p.NombreEstudiante, query) ||
                Contiene(p.CodigoEstudiante, query) ||
                Contiene(p.Docente, query))
            .ToList();
    }

    public async Task<List<Prestamo>> FiltrarPorSemestreAsync(string? semestre, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(semestre))
        {
            return await ObtenerTodosAsync(ct);
        }

        var semestreNormalizado = semestre.Trim();
        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(p => string.Equals(p.Semestre, semestreNormalizado, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<Prestamo>> FiltrarPorRangoFechasAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        if (desde is null || hasta is null)
        {
            return await ObtenerTodosAsync(ct);
        }

        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(p =>
            {
                var fechaP = DateOnly.FromDateTime(p.FechaPrestamo);
                return fechaP >= desde.Value && fechaP <= hasta.Value;
            })
            .ToList();
    }

    public async Task<List<EquipoConteo>> EquiposMasPrestadosAsync(int top, CancellationToken ct = default)
    {
        try
        {
            var todosDetalles = await _prestamoRepo.ObtenerTodosDetallesAsync(ct);
            return todosDetalles
                .GroupBy(d => d.EquipoCodigo)
                .Select(g => new EquipoConteo(g.Key, g.Count()))
                // Desempate estable por código (Java depende del orden de iteración no
                // determinista de HashMap para los empates); usar el código como
                // criterio secundario hace el resultado reproducible y comprobable.
                .OrderByDescending(c => c.Cantidad)
                .ThenBy(c => c.CodigoEquipo, StringComparer.OrdinalIgnoreCase)
                .Take(top)
                .ToList();
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Error al obtener equipos más prestados.");
            return new List<EquipoConteo>();
        }
    }

    public async Task<List<PrestamosPorMesItem>> PrestamosPorMesAsync(int meses, CancellationToken ct = default)
    {
        var prestamos = await ObtenerTodosAsync(ct);
        var agrupado = prestamos
            .GroupBy(p => (p.FechaPrestamo.Year, p.FechaPrestamo.Month))
            .ToDictionary(g => g.Key, g => g.Count());

        var resultado = new List<PrestamosPorMesItem>();
        var actual = DateTime.Now;
        for (var i = meses - 1; i >= 0; i--)
        {
            var fecha = actual.AddMonths(-i);
            var clave = (fecha.Year, fecha.Month);
            var cantidad = agrupado.GetValueOrDefault(clave, 0);
            resultado.Add(new PrestamosPorMesItem(fecha.Year, fecha.Month, cantidad));
        }

        return resultado;
    }

    public async Task<long> ContarActivosAsync(CancellationToken ct = default)
    {
        var activos = await ObtenerActivosAsync(ct);
        return activos.Count;
    }

    public async Task<long> ContarDevueltosHoyAsync(CancellationToken ct = default)
    {
        // Fecha local del sistema (DateTime.Now), no UTC: igual que Java, que usa
        // LocalDate.now() con ZoneId.systemDefault().
        var hoy = DateTime.Now.Date;
        var todos = await ObtenerTodosAsync(ct);
        return todos.Count(p =>
            p.Estado == EstadoPrestamo.Devuelto &&
            p.FechaDevolucion.HasValue &&
            p.FechaDevolucion.Value.Date == hoy);
    }

    private static bool Contiene(string? valor, string query) =>
        !string.IsNullOrEmpty(valor) && valor.Contains(query, StringComparison.OrdinalIgnoreCase);
}
