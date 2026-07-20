using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SisLabTopo.Data;

namespace SisLabTopo.Data.Tests;

/// <summary>
/// Base para pruebas de integración contra un archivo SQLite temporal real (no
/// mocks), espejando el espíritu de <c>ExcelEquipoRepositoryTest.java</c>
/// (@BeforeEach/@AfterEach con un archivo temporal por prueba). xUnit crea una
/// instancia nueva de la clase de prueba por cada método [Fact]/[Theory], así que
/// <see cref="InitializeAsync"/>/<see cref="DisposeAsync"/> ya dan, de forma natural,
/// un directorio temporal por test con limpieza automática.
/// </summary>
public abstract class SqliteTestBase : IAsyncLifetime
{
    private string _tempDir = string.Empty;
    protected string DbPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sislabtopo_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        DbPath = Path.Combine(_tempDir, "test.db");

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        // Microsoft.Data.Sqlite agrupa (pool) conexiones por cadena de conexión; sin
        // esto el archivo .db queda bloqueado en Windows aunque todos los DbContext ya
        // se hayan desechado, y el borrado del directorio temporal falla con IOException.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Crea un nuevo DbContext apuntando al mismo archivo SQLite temporal. Cada test
    /// debe crear los contextos que necesite y desecharlos (using/await using).</summary>
    protected SisLabTopoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SisLabTopoDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options;
        return new SisLabTopoDbContext(options);
    }
}
