using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SisLabTopo.Data;

/// <summary>
/// Inicializador de la base de datos SQLite. Análogo a
/// <c>repository.excel.DatabaseInitializer</c> (Java): se encarga de crear el
/// directorio/archivo si no existe y de dejar el esquema al día. A diferencia de la
/// versión Java (que copiaba una plantilla .xlsx desde recursos), aquí basta con
/// aplicar las migraciones de EF Core — SQLite crea el archivo automáticamente en el
/// primer <c>Migrate()</c> si no existe.
/// </summary>
public class DatabaseInitializer
{
    private readonly SisLabTopoDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(SisLabTopoDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crea el directorio contenedor si hace falta y aplica todas las migraciones
    /// pendientes (incluida la creación inicial del esquema si la base de datos es
    /// nueva). Es seguro invocarlo en cada arranque de la aplicación.
    /// </summary>
    public async Task InicializarSiNoExisteAsync(CancellationToken ct = default)
    {
        var dataSource = _context.Database.GetDbConnection().DataSource;
        if (!string.IsNullOrEmpty(dataSource))
        {
            var directorio = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
            {
                Directory.CreateDirectory(directorio);
                _logger.LogInformation("Directorio de base de datos creado: {Directorio}", directorio);
            }
        }

        var pendientes = (await _context.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pendientes.Count > 0)
        {
            _logger.LogInformation(
                "Aplicando {Cantidad} migración(es) pendiente(s): {Migraciones}",
                pendientes.Count, string.Join(", ", pendientes));
        }
        else
        {
            _logger.LogInformation("Base de datos SQLite encontrada y al día en: {DataSource}", dataSource);
        }

        await _context.Database.MigrateAsync(ct);
    }
}
