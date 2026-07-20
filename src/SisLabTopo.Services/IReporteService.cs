namespace SisLabTopo.Services;

/// <summary>
/// Servicio de generación de reportes en Excel. Puerto 1:1 de
/// <c>service.ReporteService</c> (Java) usando ClosedXML en vez de Apache POI.
/// </summary>
public interface IReporteService
{
    /// <summary>
    /// Exporta el inventario completo de equipos a un archivo .xlsx temporal (hoja
    /// "Inventario de Equipos") y devuelve la ruta al archivo generado.
    /// </summary>
    Task<string> ExportarInventarioExcelAsync(CancellationToken ct = default);

    /// <summary>
    /// Exporta el historial de préstamos cuya fecha de préstamo cae dentro de
    /// [<paramref name="desde"/>, <paramref name="hasta"/>] (inclusive) a un archivo
    /// .xlsx temporal (hoja "Historial de Préstamos") y devuelve la ruta al archivo
    /// generado.
    /// </summary>
    Task<string> ExportarHistorialExcelAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}
