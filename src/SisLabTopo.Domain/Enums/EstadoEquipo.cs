using Microsoft.Extensions.Logging;

namespace SisLabTopo.Domain.Enums;

/// <summary>
/// Representa el estado físico o de conservación de un equipo topográfico.
/// Puerto 1:1 de <c>model.enums.EstadoEquipo</c> (Java).
/// </summary>
public enum EstadoEquipo
{
    Bueno,
    Regular,
    Malo,
    Nuevo,
    Desuso,
    Chatarra
}

/// <summary>
/// Etiquetas en español, colores y parseo tolerante para <see cref="EstadoEquipo"/>.
/// </summary>
public static class EstadoEquipoExtensions
{
    public const EstadoEquipo ValorPorDefecto = EstadoEquipo.Bueno;

    /// <summary>Etiqueta en español para mostrar en la interfaz de usuario.</summary>
    public static string Etiqueta(this EstadoEquipo estado) => estado switch
    {
        EstadoEquipo.Bueno => "Bueno",
        EstadoEquipo.Regular => "Regular",
        EstadoEquipo.Malo => "Malo",
        EstadoEquipo.Nuevo => "Nuevo",
        EstadoEquipo.Desuso => "Desuso",
        EstadoEquipo.Chatarra => "Chatarra",
        _ => estado.ToString()
    };

    /// <summary>Color hexadecimal asociado (idéntico al de la versión Java).</summary>
    public static string ColorHex(this EstadoEquipo estado) => estado switch
    {
        EstadoEquipo.Bueno => "#28a745",
        EstadoEquipo.Regular => "#ffc107",
        EstadoEquipo.Malo => "#dc3545",
        EstadoEquipo.Nuevo => "#17a2b8",
        EstadoEquipo.Desuso => "#6c757d",
        EstadoEquipo.Chatarra => "#721c24",
        _ => "#28a745"
    };

    /// <summary>
    /// Obtiene el enum correspondiente a partir de una cadena (ignorando mayúsculas/minúsculas),
    /// aceptando tanto el nombre del enum como su etiqueta en español. Replica el comportamiento
    /// de <c>EstadoEquipo.fromString</c> en Java: si no se reconoce el valor, cae silenciosamente
    /// al valor por defecto (<see cref="ValorPorDefecto"/>) — salvo que ahora, a diferencia de Java,
    /// se registra una advertencia vía <paramref name="logger"/> cuando eso ocurre por un valor no
    /// vacío no reconocido (mejora acordada en el plan de migración, sin romper compatibilidad).
    /// </summary>
    public static EstadoEquipo FromString(string? valor, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return ValorPorDefecto;
        }

        var trimmed = valor.Trim();
        var trimmedSinGuionBajo = trimmed.Replace("_", string.Empty);
        foreach (var estado in Enum.GetValues<EstadoEquipo>())
        {
            if (string.Equals(estado.ToString(), trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado.ToString(), trimmedSinGuionBajo, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado.Etiqueta(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return estado;
            }
        }

        logger?.LogWarning(
            "Valor de EstadoEquipo no reconocido: '{Valor}'. Se usará el valor por defecto '{Default}'.",
            valor, ValorPorDefecto);
        return ValorPorDefecto;
    }
}
