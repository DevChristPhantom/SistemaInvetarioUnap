using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace SisLabTopo.Data.Tests;

/// <summary>
/// Prueba de humo: <see cref="DatabaseInitializer"/> debe crear el archivo .db (si no
/// existe) y dejar aplicado el esquema completo de la migración InitialCreate.
/// </summary>
public class DatabaseInitializerTests : IAsyncLifetime
{
    private string _tempDir = string.Empty;
    private string _dbPath = string.Empty;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sislabtopo_tests_init_" + Guid.NewGuid().ToString("N"));
        _dbPath = Path.Combine(_tempDir, "nested", "sislabtopo.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task InicializarSiNoExiste_DeberiaCrearDirectorioArchivoYEsquema()
    {
        Assert.False(File.Exists(_dbPath));

        var options = new DbContextOptionsBuilder<SisLabTopoDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        await using var context = new SisLabTopoDbContext(options);
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);

        await initializer.InicializarSiNoExisteAsync();

        Assert.True(File.Exists(_dbPath));

        // El esquema debe existir: las 5 tablas deben ser consultables sin error,
        // y AppState debe tener la fila semilla (Id = 1).
        Assert.Empty(await context.Equipos.ToListAsync());
        Assert.Empty(await context.Prestamos.ToListAsync());
        Assert.Empty(await context.DetallesPrestamo.ToListAsync());
        Assert.Empty(await context.ConfigEntries.ToListAsync());

        var estado = await context.AppStates.SingleAsync();
        Assert.Equal(1, estado.Id);
        Assert.Equal(0, estado.IntentosFallidos);
        Assert.Null(estado.HoraBloqueoUtc);

        var pendientes = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendientes);
    }

    [Fact]
    public async Task InicializarSiNoExiste_EsIdempotente()
    {
        var options = new DbContextOptionsBuilder<SisLabTopoDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        await using var context = new SisLabTopoDbContext(options);
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);

        await initializer.InicializarSiNoExisteAsync();
        // Segunda llamada no debe fallar ni duplicar la fila semilla de AppState.
        await initializer.InicializarSiNoExisteAsync();

        var cantidadEstados = await context.AppStates.CountAsync();
        Assert.Equal(1, cantidadEstados);
    }
}
