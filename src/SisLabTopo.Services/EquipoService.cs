using ClosedXML.Excel;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services;

/// <inheritdoc cref="IEquipoService"/>
public class EquipoService : IEquipoService
{
    private readonly IEquipoRepository _repository;
    private readonly IValidator<Equipo> _validator;
    private readonly ILogger<EquipoService> _logger;

    public EquipoService(IEquipoRepository repository, IValidator<Equipo> validator, ILogger<EquipoService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<List<Equipo>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _repository.ObtenerTodosAsync(ct);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Error al obtener todos los equipos.");
            return new List<Equipo>();
        }
    }

    public async Task<List<Equipo>> ObtenerDisponiblesAsync(CancellationToken ct = default)
    {
        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(eq => eq.Disponible).ToList();
    }

    public async Task<List<Equipo>> BuscarPorTextoAsync(string? texto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return await ObtenerTodosAsync(ct);
        }

        var query = texto.Trim();
        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(eq =>
                Contiene(eq.Codigo, query) ||
                Contiene(eq.Denominacion, query) ||
                Contiene(eq.Marca, query) ||
                Contiene(eq.Modelo, query) ||
                Contiene(eq.Serie, query))
            .ToList();
    }

    public async Task<List<Equipo>> FiltrarPorEstadoAsync(EstadoEquipo? estado, CancellationToken ct = default)
    {
        if (estado is null)
        {
            return await ObtenerTodosAsync(ct);
        }

        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(eq => eq.Estado == estado.Value).ToList();
    }

    public async Task<List<Equipo>> FiltrarPorTipoAsync(TipoEquipo? tipo, CancellationToken ct = default)
    {
        if (tipo is null)
        {
            return await ObtenerTodosAsync(ct);
        }

        var todos = await ObtenerTodosAsync(ct);
        return todos.Where(eq => eq.Tipo == tipo.Value).ToList();
    }

    public async Task<Equipo?> BuscarPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            return await _repository.BuscarPorCodigoAsync(codigo, ct);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Error al buscar equipo por código: {Codigo}", codigo);
            return null;
        }
    }

    public async Task RegistrarAsync(Equipo equipo, CancellationToken ct = default)
    {
        ValidationHelper.Validar(_validator, equipo);

        var existente = await _repository.BuscarPorCodigoAsync(equipo.Codigo, ct);
        if (existente is not null)
        {
            throw new ServiceException(ErrorCode.DatosInvalidos, "El código patrimonial ya se encuentra registrado.");
        }

        await _repository.GuardarAsync(equipo, ct);
    }

    public async Task ActualizarAsync(Equipo equipo, CancellationToken ct = default)
    {
        ValidationHelper.Validar(_validator, equipo);

        var existente = await _repository.BuscarPorCodigoAsync(equipo.Codigo, ct);
        if (existente is null)
        {
            throw new ServiceException(ErrorCode.EquipoNoEncontrado, "El equipo a actualizar no existe.");
        }

        await _repository.ActualizarAsync(equipo, ct);
    }

    public async Task EliminarAsync(string codigo, CancellationToken ct = default)
    {
        var equipo = await _repository.BuscarPorCodigoAsync(codigo, ct);
        if (equipo is null)
        {
            throw new ServiceException(ErrorCode.EquipoNoEncontrado, "El equipo no existe.");
        }

        if (!equipo.Disponible)
        {
            throw new ServiceException(
                ErrorCode.EquipoEnPrestamoActivo,
                "No se puede eliminar el equipo porque se encuentra en un préstamo activo.");
        }

        await _repository.EliminarAsync(codigo, ct);
    }

    public async Task<Dictionary<EstadoEquipo, int>> ContarPorEstadoAsync(CancellationToken ct = default)
    {
        var todos = await ObtenerTodosAsync(ct);
        return todos.GroupBy(eq => eq.Estado).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<TipoEquipo, int>> ContarPorTipoAsync(CancellationToken ct = default)
    {
        var todos = await ObtenerTodosAsync(ct);
        return todos.GroupBy(eq => eq.Tipo).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task ImportarDesdeExcelAsync(string rutaArchivo, CancellationToken ct = default)
    {
        // Guarda de path traversal: igual que Java (File.isAbsolute() + !path.contains("..")),
        // usando Path.IsPathFullyQualified (más estricto que IsPathRooted en Windows: exige
        // unidad + raíz, igual que File.isAbsolute() en esa plataforma) para rechazar rutas
        // relativas o ambiguas además de cualquier ruta que contenga "..".
        if (string.IsNullOrWhiteSpace(rutaArchivo) || !Path.IsPathFullyQualified(rutaArchivo) || rutaArchivo.Contains(".."))
        {
            throw new ServiceException(ErrorCode.ArchivoExcelInvalido, "Ruta de archivo inválida o no permitida (Path Traversal guard).");
        }

        if (!File.Exists(rutaArchivo))
        {
            throw new ServiceException(ErrorCode.ArchivoExcelInvalido, "El archivo seleccionado no existe.");
        }

        try
        {
            using var workbook = new XLWorkbook(rutaArchivo);
            if (workbook.Worksheets.Count == 0)
            {
                throw new ServiceException(ErrorCode.ArchivoExcelInvalido, "El archivo Excel no contiene hojas utilizables.");
            }

            var sheet = workbook.Worksheets.First();
            var rangoUsado = sheet.RangeUsed();
            if (rangoUsado is null)
            {
                return;
            }

            foreach (var row in rangoUsado.RowsUsed())
            {
                if (row.RowNumber() == 1)
                {
                    continue; // fila de cabecera
                }

                var codigo = GetString(row.Cell(1));
                var denominacion = GetString(row.Cell(2));
                if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(denominacion))
                {
                    continue; // saltar filas incompletas
                }

                var equipo = new Equipo
                {
                    Codigo = codigo,
                    Denominacion = denominacion,
                    Modelo = GetString(row.Cell(3)),
                    Marca = GetString(row.Cell(4)),
                    Serie = GetString(row.Cell(5)),
                    Estado = EstadoEquipoExtensions.FromString(GetString(row.Cell(6))),
                    Tipo = TipoEquipoExtensions.FromString(GetString(row.Cell(7))),
                    Disponible = true,
                    Observacion = GetString(row.Cell(8)),
                    FechaRegistro = DateTime.Now
                };

                try
                {
                    var existente = await _repository.BuscarPorCodigoAsync(codigo, ct);
                    if (existente is not null)
                    {
                        await _repository.ActualizarAsync(equipo, ct);
                    }
                    else
                    {
                        await _repository.GuardarAsync(equipo, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fallo al importar la fila {Fila} del Excel externo.", row.RowNumber());
                }
            }
        }
        catch (ServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo general al importar desde Excel.");
            throw new ServiceException(ErrorCode.ArchivoExcelInvalido, "Error de lectura en el archivo Excel de importación.", ex);
        }
    }

    private static bool Contiene(string? valor, string query) =>
        !string.IsNullOrEmpty(valor) && valor.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string GetString(IXLCell cell) => cell.IsEmpty() ? string.Empty : cell.GetString().Trim();
}
