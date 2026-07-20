using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisLabTopo.Data.Import;
using SisLabTopo.Data.Repositories;

namespace SisLabTopo.Data;

/// <summary>
/// Registro en el contenedor de DI (<c>Microsoft.Extensions.Hosting</c>) de todo lo
/// que ofrece esta capa: DbContext, repositorios, unit-of-work, inicializador e
/// importador de datos legados. Se usará desde <c>SisLabTopo.UI</c> (host WPF) a
/// partir de la Fase 4/5; se añade ya para dejar la capa de datos lista para
/// cablearse sin cambios futuros.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSisLabTopoData(this IServiceCollection services, string? connectionString = null)
    {
        var cs = connectionString ?? DatabasePathProvider.GetDefaultConnectionString();

        services.AddDbContext<SisLabTopoDbContext>(options => options.UseSqlite(cs));
        services.AddScoped<IEquipoRepository, EquipoRepository>();
        services.AddScoped<IPrestamoRepository, PrestamoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<LegacyExcelImporter>();

        return services;
    }
}
