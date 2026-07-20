using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services;

/// <summary>
/// Registro en el contenedor de DI de todos los servicios de negocio de la Fase 2
/// (Auth/Equipo/Prestamo/Reporte) y sus validadores de FluentValidation. Se añade ya,
/// siguiendo el mismo patrón que <c>SisLabTopo.Data.ServiceCollectionExtensions</c>,
/// para dejar la capa lista para cablearse desde <c>SisLabTopo.UI</c> sin cambios
/// futuros (Fase 4/5).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSisLabTopoServices(this IServiceCollection services)
    {
        services.AddScoped<IValidator<Equipo>, EquipoValidator>();
        services.AddScoped<IValidator<Prestamo>, PrestamoValidator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEquipoService, EquipoService>();
        services.AddScoped<IPrestamoService, PrestamoService>();
        services.AddScoped<IReporteService, ReporteService>();

        return services;
    }
}
