namespace SisLabTopo.Data;

/// <summary>
/// Resuelve la ubicación del archivo SQLite de la aplicación. Análogo a
/// <c>config.DatabaseConfig</c> (Java), simplificado por indicación explícita del
/// plan de migración: siempre <c>%APPDATA%\SisLabTopo\sislabtopo.db</c> (Java además
/// prefería un archivo junto al ejecutable si existía; esa variante "portable" no se
/// replica aquí — puede añadirse luego si se necesita).
/// </summary>
public static class DatabasePathProvider
{
    private const string AppDirName = "SisLabTopo";
    private const string DbFileName = "sislabtopo.db";

    /// <summary>Ruta completa del archivo .db bajo %APPDATA%\SisLabTopo.</summary>
    public static string GetDefaultDbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(appData, AppDirName, DbFileName);
    }

    /// <summary>Cadena de conexión SQLite para la ruta por defecto.</summary>
    public static string GetDefaultConnectionString() => $"Data Source={GetDefaultDbPath()}";
}
