using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SisLabTopo.Data.Entities;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Import;

/// <summary>
/// Importa datos legados desde el archivo <c>labtopoMinera_db.xlsx</c> (el "motor de
/// almacenamiento" de la versión Java) hacia la base de datos SQLite, cuando esta
/// última está recién creada/vacía. Lee las 4 hojas originales (EQUIPOS, PRESTAMOS,
/// DETALLE_PRESTAMO, CONFIG) con el mismo layout de columnas que
/// <c>util.ExcelUtils.inicializarBaseDatos</c> definía en Java.
///
/// No se cablea todavía a la UI (eso es Fase 5) — esta clase es invocable de forma
/// independiente y solo requiere una ruta de archivo .xlsx.
/// </summary>
public class LegacyExcelImporter
{
    private const string HojaEquipos = "EQUIPOS";
    private const string HojaPrestamos = "PRESTAMOS";
    private const string HojaDetalle = "DETALLE_PRESTAMO";
    private const string HojaConfig = "CONFIG";

    private readonly SisLabTopoDbContext _context;
    private readonly ILogger<LegacyExcelImporter> _logger;

    public LegacyExcelImporter(SisLabTopoDbContext context, ILogger<LegacyExcelImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Importa el archivo indicado únicamente si existe y la base de datos SQLite
    /// todavía no tiene equipos ni préstamos registrados (heurística de "recién
    /// creada/vacía"). Si cualquiera de esas condiciones no se cumple, no hace nada y
    /// devuelve un resultado "omitido" con el motivo.
    /// </summary>
    public async Task<LegacyImportResult> ImportarSiCorrespondeAsync(string rutaXlsx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rutaXlsx) || !File.Exists(rutaXlsx))
        {
            _logger.LogInformation("No se encontró archivo Excel legado en '{Ruta}'. Se omite la importación.", rutaXlsx);
            return LegacyImportResult.Omitido($"Archivo no encontrado: {rutaXlsx}");
        }

        var hayEquipos = await _context.Equipos.AnyAsync(ct);
        var hayPrestamos = await _context.Prestamos.AnyAsync(ct);
        if (hayEquipos || hayPrestamos)
        {
            _logger.LogInformation("La base de datos SQLite ya contiene datos. Se omite la importación legada.");
            return LegacyImportResult.Omitido("La base de datos ya contiene datos.");
        }

        return await ImportarAsync(rutaXlsx, ct);
    }

    /// <summary>Importa incondicionalmente el archivo indicado (sin verificar si la base está vacía).</summary>
    public async Task<LegacyImportResult> ImportarAsync(string rutaXlsx, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(rutaXlsx);

        var equipos = ImportarEquipos(workbook);
        var prestamos = ImportarPrestamos(workbook);
        var detalles = ImportarDetalles(workbook);
        var configEntries = ImportarConfig(workbook);

        _context.Equipos.AddRange(equipos);
        _context.Prestamos.AddRange(prestamos);
        _context.DetallesPrestamo.AddRange(detalles);
        _context.ConfigEntries.AddRange(configEntries);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Importación legada completada: {Equipos} equipos, {Prestamos} préstamos, {Detalles} detalles, {Config} parámetros de configuración.",
            equipos.Count, prestamos.Count, detalles.Count, configEntries.Count);

        return LegacyImportResult.Exitoso(equipos.Count, prestamos.Count, detalles.Count, configEntries.Count);
    }

    private List<Equipo> ImportarEquipos(XLWorkbook workbook)
    {
        var resultado = new List<Equipo>();
        if (!workbook.TryGetWorksheet(HojaEquipos, out var hoja))
        {
            return resultado;
        }

        foreach (var row in FilasDeDatos(hoja))
        {
            var codigo = GetString(row.Cell(1));
            if (string.IsNullOrWhiteSpace(codigo))
            {
                continue;
            }

            resultado.Add(new Equipo
            {
                Codigo = codigo,
                Denominacion = GetString(row.Cell(2)),
                Modelo = NullIfEmpty(GetString(row.Cell(3))),
                Marca = NullIfEmpty(GetString(row.Cell(4))),
                Serie = NullIfEmpty(GetString(row.Cell(5))),
                Estado = EstadoEquipoExtensions.FromString(GetString(row.Cell(6))),
                Tipo = TipoEquipoExtensions.FromString(GetString(row.Cell(7))),
                Disponible = GetBool(row.Cell(8)),
                Observacion = NullIfEmpty(GetString(row.Cell(9))),
                FechaRegistro = GetDateTime(row.Cell(10)) ?? DateTime.Now
            });
        }

        return resultado;
    }

    private List<Prestamo> ImportarPrestamos(XLWorkbook workbook)
    {
        var resultado = new List<Prestamo>();
        if (!workbook.TryGetWorksheet(HojaPrestamos, out var hoja))
        {
            return resultado;
        }

        foreach (var row in FilasDeDatos(hoja))
        {
            var id = GetString(row.Cell(1));
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            resultado.Add(new Prestamo
            {
                Id = id,
                Docente = GetString(row.Cell(2)),
                Curso = GetString(row.Cell(3)),
                Semestre = GetString(row.Cell(4)),
                NombreEstudiante = GetString(row.Cell(5)),
                CodigoEstudiante = GetString(row.Cell(6)),
                FechaPrestamo = GetDateTime(row.Cell(7)) ?? DateTime.Now,
                FechaDevolucion = GetDateTime(row.Cell(8)),
                Estado = EstadoPrestamoExtensions.FromString(GetString(row.Cell(9))),
                Observaciones = NullIfEmpty(GetString(row.Cell(10))),
                FechaRegistro = GetDateTime(row.Cell(11)) ?? DateTime.Now
            });
        }

        return resultado;
    }

    private List<DetallePrestamo> ImportarDetalles(XLWorkbook workbook)
    {
        var resultado = new List<DetallePrestamo>();
        if (!workbook.TryGetWorksheet(HojaDetalle, out var hoja))
        {
            return resultado;
        }

        foreach (var row in FilasDeDatos(hoja))
        {
            var id = GetString(row.Cell(1));
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            resultado.Add(new DetallePrestamo
            {
                Id = id,
                PrestamoId = GetString(row.Cell(2)),
                EquipoCodigo = GetString(row.Cell(3)),
                ObservacionItem = NullIfEmpty(GetString(row.Cell(4))),
                Devuelto = GetBool(row.Cell(5))
            });
        }

        return resultado;
    }

    private List<ConfigEntry> ImportarConfig(XLWorkbook workbook)
    {
        var resultado = new List<ConfigEntry>();
        if (!workbook.TryGetWorksheet(HojaConfig, out var hoja))
        {
            return resultado;
        }

        foreach (var row in FilasDeDatos(hoja))
        {
            var clave = GetString(row.Cell(1));
            if (string.IsNullOrWhiteSpace(clave))
            {
                continue;
            }

            resultado.Add(new ConfigEntry
            {
                Clave = clave,
                Valor = NullIfEmpty(GetString(row.Cell(2)))
            });
        }

        return resultado;
    }

    /// <summary>Filas de datos de una hoja, saltando la fila de cabeceras (fila 1).</summary>
    private static IEnumerable<IXLRangeRow> FilasDeDatos(IXLWorksheet hoja)
    {
        var rango = hoja.RangeUsed();
        if (rango is null)
        {
            yield break;
        }

        foreach (var row in rango.RowsUsed())
        {
            if (row.RowNumber() == 1)
            {
                continue; // cabecera
            }

            yield return row;
        }
    }

    private static string GetString(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        return cell.GetString().Trim();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool GetBool(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return false;
        }

        if (cell.TryGetValue(out bool boolValue))
        {
            return boolValue;
        }

        var text = cell.GetString().Trim();
        return bool.TryParse(text, out var parsed) && parsed || text == "1";
    }

    private static DateTime? GetDateTime(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue(out DateTime dateValue))
        {
            return dateValue;
        }

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTime.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
