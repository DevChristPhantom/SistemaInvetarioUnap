namespace SisLabTopo.Data.Import;

/// <summary>Resultado de un intento de importación de datos legados desde el .xlsx.</summary>
public sealed record LegacyImportResult(
    bool Importado,
    int Equipos,
    int Prestamos,
    int Detalles,
    int ConfigEntries,
    string? Motivo)
{
    public static LegacyImportResult Omitido(string motivo) => new(false, 0, 0, 0, 0, motivo);

    public static LegacyImportResult Exitoso(int equipos, int prestamos, int detalles, int configEntries) =>
        new(true, equipos, prestamos, detalles, configEntries, null);
}
