using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Data;
using SisLabTopo.Data.Repositories;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Base para pruebas de integración de servicios contra un archivo SQLite temporal
/// real (no mocks) — usada específicamente para los flujos donde la mejora de la Fase 2
/// (transacciones EF Core reales para Registrar Préstamo/Devolución, y persistencia
/// real del bloqueo de login) es la propiedad bajo prueba, y por lo tanto mockear los
/// repositorios ocultaría justo lo que se quiere verificar. Espeja
/// <c>SisLabTopo.Data.Tests.SqliteTestBase</c>.
/// </summary>
public abstract class ServicesSqliteTestBase : IAsyncLifetime
{
    private string _tempDir = string.Empty;
    protected string DbPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sislabtopo_svc_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        DbPath = Path.Combine(_tempDir, "test.db");

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public virtual Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Crea un nuevo DbContext apuntando al mismo archivo SQLite temporal.</summary>
    protected SisLabTopoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SisLabTopoDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options;
        return new SisLabTopoDbContext(options);
    }

    protected static EquipoRepository CreateEquipoRepo(SisLabTopoDbContext context) =>
        new(context, NullLogger<EquipoRepository>.Instance);

    protected static PrestamoRepository CreatePrestamoRepo(SisLabTopoDbContext context) =>
        new(context, NullLogger<PrestamoRepository>.Instance);

    protected static UnitOfWork CreateUnitOfWork(SisLabTopoDbContext context) => new(context);
}
