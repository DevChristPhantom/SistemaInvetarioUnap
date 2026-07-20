using Microsoft.Extensions.Logging;

namespace SisLabTopo.Domain.Enums;

/// <summary>
/// Representa el estado de un préstamo de equipo.
/// Puerto 1:1 de <c>model.enums.EstadoPrestamo</c> (Java).
/// </summary>
public enum EstadoPrestamo
{
    Activo,
    Devuelto,
    Vencido
}

/// <summary>
/// Etiquetas en español y parseo tolerante para <see cref="EstadoPrestamo"/>.
/// </summary>
public static class EstadoPrestamoExtensions
{
    public const EstadoPrestamo ValorPorDefecto = EstadoPrestamo.Activo;

    public static string Etiqueta(this EstadoPrestamo estado) => estado switch
    {
        EstadoPrestamo.Activo => "Activo",
        EstadoPrestamo.Devuelto => "Devuelto",
        EstadoPrestamo.Vencido => "Vencido",
        _ => estado.ToString()
    };

    /// <summary>
    /// Obtiene el enum correspondiente a partir de una cadena (ignorando mayúsculas/minúsculas),
    /// con fallback silencioso a <see cref="ValorPorDefecto"/> (más advertencia opcional vía
    /// <paramref name="logger"/>), replicando <c>EstadoPrestamo.fromString</c> en Java.
    /// </summary>
    public static EstadoPrestamo FromString(string? valor, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return ValorPorDefecto;
        }

        var trimmed = valor.Trim();
        var trimmedSinGuionBajo = trimmed.Replace("_", string.Empty);
        foreach (var estado in Enum.GetValues<EstadoPrestamo>())
        {
            if (string.Equals(estado.ToString(), trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado.ToString(), trimmedSinGuionBajo, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado.Etiqueta(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return estado;
            }
        }

        logger?.LogWarning(
            "Valor de EstadoPrestamo no reconocido: '{Valor}'. Se usará el valor por defecto '{Default}'.",
            valor, ValorPorDefecto);
        return ValorPorDefecto;
    }
}
