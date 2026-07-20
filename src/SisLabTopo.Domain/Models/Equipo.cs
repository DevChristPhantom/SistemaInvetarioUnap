using SisLabTopo.Domain.Enums;

namespace SisLabTopo.Domain.Models;

/// <summary>
/// Representa un equipo topográfico del laboratorio.
/// Puerto 1:1 de <c>model.Equipo</c> (Java): mismos campos, mismo significado.
/// </summary>
public class Equipo
{
    /// <summary>Código patrimonial único del equipo. Clave primaria.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Denominacion { get; set; } = string.Empty;

    public string? Modelo { get; set; }

    public string? Marca { get; set; }

    public string? Serie { get; set; }

    public EstadoEquipo Estado { get; set; } = EstadoEquipoExtensions.ValorPorDefecto;

    public TipoEquipo Tipo { get; set; } = TipoEquipoExtensions.ValorPorDefecto;

    public bool Disponible { get; set; }

    public string? Observacion { get; set; }

    public DateTime FechaRegistro { get; set; }

    /// <summary>Detalles de préstamo (histórico) que referencian este equipo.</summary>
    public ICollection<DetallePrestamo> DetallesPrestamo { get; set; } = new List<DetallePrestamo>();
}
