using SisLabTopo.Domain.Models;

namespace SisLabTopo.Reports.Tests;

/// <summary>Constructores compartidos de datos de ejemplo para las pruebas del comprobante PDF.</summary>
internal static class ComprobanteTestFixtures
{
    public static Prestamo CrearPrestamo(
        string? docente = "Ing. Juan Pérez Quispe",
        string? curso = "Topografía II",
        string? semestre = "2026-I",
        string? nombreEstudiante = "María Fernanda Huanca Condori",
        string? codigoEstudiante = "201812345",
        string? observaciones = "Sin observaciones.")
    {
        return new Prestamo
        {
            Id = Guid.NewGuid().ToString(),
            Docente = docente ?? string.Empty,
            Curso = curso ?? string.Empty,
            Semestre = semestre ?? string.Empty,
            NombreEstudiante = nombreEstudiante ?? string.Empty,
            CodigoEstudiante = codigoEstudiante ?? string.Empty,
            FechaPrestamo = DateTime.Today,
            Observaciones = observaciones,
            FechaRegistro = DateTime.Now,
        };
    }

    public static DetallePrestamo CrearDetalle(string prestamoId, string equipoCodigo) => new()
    {
        Id = Guid.NewGuid().ToString(),
        PrestamoId = prestamoId,
        EquipoCodigo = equipoCodigo,
    };

    public static Equipo CrearEquipo(string codigo, string denominacion) => new()
    {
        Codigo = codigo,
        Denominacion = denominacion,
        FechaRegistro = DateTime.Now,
    };

    /// <summary>Catálogo de equipos típico del laboratorio, usado en varias pruebas.</summary>
    public static List<Equipo> CatalogoEquipos() =>
    [
        CrearEquipo("EST-001", "Estación Total Topcon GPT-3000"),
        CrearEquipo("NIV-002", "Nivel Automático Sokkia C32"),
        CrearEquipo("TRIP-003", "Trípode de aluminio"),
        CrearEquipo("PRIS-004", "Prisma reflector simple"),
        CrearEquipo("JAL-005", "Jalón telescópico"),
        CrearEquipo("CIN-006", "Cinta métrica de 50m"),
        CrearEquipo("GPS-007", "GPS diferencial Trimble"),
    ];
}
