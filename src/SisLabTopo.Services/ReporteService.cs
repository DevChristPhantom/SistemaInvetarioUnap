using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;

namespace SisLabTopo.Services;

/// <inheritdoc cref="IReporteService"/>
public class ReporteService : IReporteService
{
    private static readonly string[] CabecerasInventario =
        { "CÓDIGO", "DENOMINACIÓN", "MODELO", "MARCA", "SERIE", "ESTADO", "TIPO", "DISPONIBLE", "OBSERVACIÓN", "FECHA REGISTRO" };

    private static readonly string[] CabecerasHistorial =
        { "ID", "DOCENTE", "CURSO", "SEMESTRE", "ESTUDIANTE", "CÓD. ESTUDIANTE", "FECHA PRÉSTAMO", "FECHA DEVOLUCIÓN", "ESTADO", "OBSERVACIONES" };

    private readonly IEquipoRepository _equipoRepo;
    private readonly IPrestamoRepository _prestamoRepo;
    private readonly ILogger<ReporteService> _logger;

    public ReporteService(IEquipoRepository equipoRepo, IPrestamoRepository prestamoRepo, ILogger<ReporteService> logger)
    {
        _equipoRepo = equipoRepo;
        _prestamoRepo = prestamoRepo;
        _logger = logger;
    }

    public async Task<string> ExportarInventarioExcelAsync(CancellationToken ct = default)
    {
        var equipos = await _equipoRepo.ObtenerTodosAsync(ct);
        var tempPath = RutaTemporal("reporte_inventario_");

        try
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Inventario de Equipos");
            EscribirCabecera(sheet, CabecerasInventario);

            var fila = 2;
            foreach (var eq in equipos)
            {
                sheet.Cell(fila, 1).Value = eq.Codigo;
                sheet.Cell(fila, 2).Value = eq.Denominacion;
                sheet.Cell(fila, 3).Value = eq.Modelo ?? string.Empty;
                sheet.Cell(fila, 4).Value = eq.Marca ?? string.Empty;
                sheet.Cell(fila, 5).Value = eq.Serie ?? string.Empty;
                sheet.Cell(fila, 6).Value = eq.Estado.Etiqueta();
                sheet.Cell(fila, 7).Value = eq.Tipo.Etiqueta();
                sheet.Cell(fila, 8).Value = eq.Disponible ? "Sí" : "No";
                sheet.Cell(fila, 9).Value = eq.Observacion ?? string.Empty;
                sheet.Cell(fila, 10).Value = eq.FechaRegistro;
                fila++;
            }

            sheet.Columns(1, CabecerasInventario.Length).AdjustToContents();
            workbook.SaveAs(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar inventario a Excel.");
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Fallo al generar archivo temporal de reporte.", ex);
        }

        return tempPath;
    }

    public async Task<string> ExportarHistorialExcelAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var prestamos = (await _prestamoRepo.ObtenerTodosAsync(ct))
            .Where(p =>
            {
                var fp = DateOnly.FromDateTime(p.FechaPrestamo);
                return fp >= desde && fp <= hasta;
            })
            .ToList();

        var tempPath = RutaTemporal("reporte_historial_");

        try
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Historial de Préstamos");
            EscribirCabecera(sheet, CabecerasHistorial);

            var fila = 2;
            foreach (var pr in prestamos)
            {
                sheet.Cell(fila, 1).Value = pr.Id;
                sheet.Cell(fila, 2).Value = pr.Docente;
                sheet.Cell(fila, 3).Value = pr.Curso;
                sheet.Cell(fila, 4).Value = pr.Semestre;
                sheet.Cell(fila, 5).Value = pr.NombreEstudiante;
                sheet.Cell(fila, 6).Value = pr.CodigoEstudiante;
                sheet.Cell(fila, 7).Value = pr.FechaPrestamo;
                if (pr.FechaDevolucion.HasValue)
                {
                    sheet.Cell(fila, 8).Value = pr.FechaDevolucion.Value;
                }
                else
                {
                    sheet.Cell(fila, 8).Value = "No devuelto";
                }

                sheet.Cell(fila, 9).Value = pr.Estado.Etiqueta();
                sheet.Cell(fila, 10).Value = pr.Observaciones ?? string.Empty;
                fila++;
            }

            sheet.Columns(1, CabecerasHistorial.Length).AdjustToContents();
            workbook.SaveAs(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar historial a Excel.");
            throw new ServiceException(ErrorCode.ErrorEscrituraBaseDatos, "Fallo al generar archivo temporal de historial.", ex);
        }

        return tempPath;
    }

    private static void EscribirCabecera(IXLWorksheet sheet, string[] cabeceras)
    {
        for (var i = 0; i < cabeceras.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = cabeceras[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
            cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
        }
    }

    private static string RutaTemporal(string prefijo) =>
        Path.Combine(Path.GetTempPath(), $"{prefijo}{Guid.NewGuid():N}.xlsx");
}
