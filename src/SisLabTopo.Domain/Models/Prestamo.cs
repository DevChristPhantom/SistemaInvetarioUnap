using SisLabTopo.Domain.Enums;

namespace SisLabTopo.Domain.Models;

/// <summary>
/// Representa una transacción de préstamo de equipos en el laboratorio.
/// Puerto 1:1 de <c>model.Prestamo</c> (Java).
/// </summary>
public class Prestamo
{
    /// <summary>Identificador único (UUID como texto). Clave primaria.</summary>
    public string Id { get; set; } = string.Empty;

    public string Docente { get; set; } = string.Empty;

    public string Curso { get; set; } = string.Empty;

    public string Semestre { get; set; } = string.Empty;

    public string NombreEstudiante { get; set; } = string.Empty;

    public string CodigoEstudiante { get; set; } = string.Empty;

    public DateTime FechaPrestamo { get; set; }

    public DateTime? FechaDevolucion { get; set; }

    public EstadoPrestamo Estado { get; set; } = EstadoPrestamoExtensions.ValorPorDefecto;

    public string? Observaciones { get; set; }

    public DateTime FechaRegistro { get; set; }

    /// <summary>Ítems (equipos individuales) prestados en esta transacción.</summary>
    public ICollection<DetallePrestamo> Detalles { get; set; } = new List<DetallePrestamo>();
}
