using Microsoft.Extensions.DependencyInjection;

namespace SisLabTopo.Reports;

/// <summary>
/// Registro en el contenedor de DI del generador de comprobantes PDF de la Fase 3, siguiendo
/// el mismo patrón que <c>SisLabTopo.Services.ServiceCollectionExtensions</c>, para dejar la
/// capa lista para cablearse desde <c>SisLabTopo.UI</c> sin cambios futuros (Fase 5).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSisLabTopoReports(this IServiceCollection services)
    {
        services.AddSingleton<IComprobantePrestamoGenerator, ComprobantePrestamoGenerator>();
        return services;
    }
}
