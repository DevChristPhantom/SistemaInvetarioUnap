using SisLabTopo.Domain.Models;

namespace SisLabTopo.Reports;

/// <summary>
/// Genera e imprime el comprobante PDF de un préstamo de equipos.
/// Puerto 1:1 de <c>print.ComprobantePrestamoPrinter</c> (Java), implementado antes
/// por <c>PDFBoxComprobantePrinter</c>.
/// </summary>
public interface IComprobantePrestamoGenerator
{
    /// <summary>
    /// Genera el comprobante PDF de un préstamo y lo guarda en un archivo temporal.
    /// </summary>
    /// <param name="prestamo">Préstamo a documentar (docente, curso, estudiante, observaciones, etc.).</param>
    /// <param name="detalles">Ítems (equipos) prestados en esta transacción; se listan en el mismo orden en la tabla del comprobante.</param>
    /// <param name="equipos">
    /// Catálogo completo (o al menos el subconjunto relevante) de equipos, usado para resolver la
    /// denominación de cada ítem por código. Se precarga una sola vez por generación — nunca se
    /// hace una consulta por fila de la tabla.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Ruta absoluta al archivo PDF temporal generado.</returns>
    Task<string> GenerarPdfAsync(
        Prestamo prestamo,
        IReadOnlyList<DetallePrestamo> detalles,
        IReadOnlyList<Equipo> equipos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera el comprobante (ver <see cref="GenerarPdfAsync"/>) y lo envía a imprimir usando el
    /// verbo "print" del sistema operativo sobre el visor de PDF asociado por defecto.
    /// </summary>
    Task ImprimirAsync(
        Prestamo prestamo,
        IReadOnlyList<DetallePrestamo> detalles,
        IReadOnlyList<Equipo> equipos,
        CancellationToken cancellationToken = default);
}
