namespace SisLabTopo.Domain.Models;

/// <summary>
/// Representa el detalle de un equipo individual prestado dentro de una transacción.
/// Puerto 1:1 de <c>model.DetallePrestamo</c> (Java).
/// </summary>
public class DetallePrestamo
{
    /// <summary>Identificador único (UUID como texto). Clave primaria.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>FK al préstamo (<see cref="Prestamo.Id"/>) al que pertenece este ítem.</summary>
    public string PrestamoId { get; set; } = string.Empty;

    /// <summary>FK al equipo (<see cref="Equipo.Codigo"/>) prestado en este ítem.</summary>
    public string EquipoCodigo { get; set; } = string.Empty;

    public string? ObservacionItem { get; set; }

    public bool Devuelto { get; set; }

    public Prestamo? Prestamo { get; set; }

    public Equipo? Equipo { get; set; }
}
