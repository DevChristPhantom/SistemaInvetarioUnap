using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SisLabTopo.Data;

/// <summary>
/// Factory de diseño usada exclusivamente por las herramientas de EF Core
/// (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>) para poder
/// construir un <see cref="SisLabTopoDbContext"/> fuera de la aplicación (que en
/// tiempo de ejecución lo registra vía DI con <c>Microsoft.Extensions.Hosting</c>,
/// aún no cableado en esta fase).
/// </summary>
public class SisLabTopoDbContextFactory : IDesignTimeDbContextFactory<SisLabTopoDbContext>
{
    public SisLabTopoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SisLabTopoDbContext>();
        optionsBuilder.UseSqlite(DatabasePathProvider.GetDefaultConnectionString());
        return new SisLabTopoDbContext(optionsBuilder.Options);
    }
}
