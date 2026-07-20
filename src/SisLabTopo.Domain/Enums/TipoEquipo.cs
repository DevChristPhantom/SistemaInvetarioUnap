using Microsoft.Extensions.Logging;

namespace SisLabTopo.Domain.Enums;

/// <summary>
/// Representa la categoría o tipo de un equipo topográfico.
/// Puerto 1:1 de <c>model.enums.TipoEquipo</c> (Java).
/// </summary>
public enum TipoEquipo
{
    EstacionTotal,
    Teodolito,
    Gps,
    Brujula,
    NivelTopografico,
    Prisma,
    JalonesMira,
    Tripode,
    Otros
}

/// <summary>
/// Etiquetas en español y parseo tolerante para <see cref="TipoEquipo"/>.
/// </summary>
public static class TipoEquipoExtensions
{
    public const TipoEquipo ValorPorDefecto = TipoEquipo.Otros;

    public static string Etiqueta(this TipoEquipo tipo) => tipo switch
    {
        TipoEquipo.EstacionTotal => "Estación Total",
        TipoEquipo.Teodolito => "Teodolito",
        TipoEquipo.Gps => "GPS/Posicionamiento",
        TipoEquipo.Brujula => "Brújula",
        TipoEquipo.NivelTopografico => "Nivel Topográfico",
        TipoEquipo.Prisma => "Prisma",
        TipoEquipo.JalonesMira => "Jalones/Mira",
        TipoEquipo.Tripode => "Trípode",
        TipoEquipo.Otros => "Otros",
        _ => tipo.ToString()
    };

    /// <summary>
    /// Obtiene el enum correspondiente a partir de una cadena (ignorando mayúsculas/minúsculas),
    /// con fallback silencioso a <see cref="ValorPorDefecto"/> (más advertencia opcional vía
    /// <paramref name="logger"/>), replicando <c>TipoEquipo.fromString</c> en Java.
    /// También acepta, sin distinción de guiones bajos, los nombres de enum
    /// SCREAMING_SNAKE_CASE originales de Java (p. ej. "ESTACION_TOTAL",
    /// "NIVEL_TOPOGRAFICO", "JALONES_MIRA") — necesario para que
    /// <c>LegacyExcelImporter</c> pueda leer los valores tal cual quedaron guardados
    /// en la columna TIPO del .xlsx legado, cuyo nombre de enum en C# ya no lleva
    /// guion bajo (<see cref="TipoEquipo.EstacionTotal"/>, etc.).
    /// </summary>
    public static TipoEquipo FromString(string? valor, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return ValorPorDefecto;
        }

        var trimmed = valor.Trim();
        var trimmedSinGuionBajo = trimmed.Replace("_", string.Empty);
        foreach (var tipo in Enum.GetValues<TipoEquipo>())
        {
            if (string.Equals(tipo.ToString(), trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tipo.ToString(), trimmedSinGuionBajo, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tipo.Etiqueta(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return tipo;
            }
        }

        logger?.LogWarning(
            "Valor de TipoEquipo no reconocido: '{Valor}'. Se usará el valor por defecto '{Default}'.",
            valor, ValorPorDefecto);
        return ValorPorDefecto;
    }
}
