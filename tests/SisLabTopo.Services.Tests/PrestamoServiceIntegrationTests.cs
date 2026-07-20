using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Pruebas de integración contra SQLite real para los dos flujos transaccionales de
/// <see cref="PrestamoService"/> — aquí es donde vive la mejora explícita de la Fase 2
/// sobre la versión Java: una transacción EF Core real (commit/rollback) en vez del
/// "deshacer uno por uno" manual que hacía <c>PrestamoServiceImpl.registrarPrestamo</c>
/// por las limitaciones del motor Excel. Mockear <see cref="IUnitOfWork"/> para probar
/// esto habría exigido simular un <c>IDbContextTransaction</c> completo sin ganar nada:
/// la propiedad bajo prueba (atomicidad real) solo se puede observar contra una base de
/// datos real.
///
/// Incluye el puerto de <c>PrestamoServiceTest.registrarDevolucion_deberiaActualizarEquiposADisponible</c>
/// (Java), ahora como prueba de integración por el mismo motivo.
/// </summary>
public class PrestamoServiceIntegrationTests : ServicesSqliteTestBase
{
    private PrestamoService CreateService(Data.SisLabTopoDbContext context) => new(
        CreatePrestamoRepo(context),
        CreateEquipoRepo(context),
        CreateUnitOfWork(context),
        new PrestamoValidator(),
        NullLogger<PrestamoService>.Instance);

    private static Equipo NuevoEquipo(string codigo, bool disponible = true) => new()
    {
        Codigo = codigo,
        Denominacion = "Estación Total",
        Modelo = "GTS-230",
        Marca = "Topcon",
        Serie = "SN-" + codigo,
        Estado = EstadoEquipo.Bueno,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = disponible,
        FechaRegistro = DateTime.Now
    };

    private static Prestamo NuevoPrestamo(string id) => new()
    {
        Id = id,
        Docente = "Abdul Tacma",
        Curso = "Topografía Minera",
        Semestre = "2026-I",
        NombreEstudiante = "Juan Perez",
        CodigoEstudiante = "160244",
        FechaPrestamo = DateTime.Now,
        Estado = EstadoPrestamo.Activo,
        Observaciones = "",
        FechaRegistro = DateTime.Now
    };

    [Fact]
    public async Task RegistrarPrestamo_ConEquiposDisponibles_MarcaEquiposNoDisponiblesYCreaDetalles()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        await equipoRepo.GuardarAsync(NuevoEquipo("EQ-A"));
        await equipoRepo.GuardarAsync(NuevoEquipo("EQ-B"));

        var service = CreateService(context);
        var prestamoId = await service.RegistrarPrestamoAsync(NuevoPrestamo("p-int-1"), new[] { "EQ-A", "EQ-B" });

        Assert.Equal("p-int-1", prestamoId);

        await using var verifyContext = CreateContext();
        var equipoA = await verifyContext.Equipos.FindAsync("EQ-A");
        var equipoB = await verifyContext.Equipos.FindAsync("EQ-B");
        Assert.False(equipoA!.Disponible);
        Assert.False(equipoB!.Disponible);

        var detalles = verifyContext.DetallesPrestamo.Where(d => d.PrestamoId == "p-int-1").ToList();
        Assert.Equal(2, detalles.Count);
        Assert.All(detalles, d => Assert.Equal("Estado al entregar: Bueno", d.ObservacionItem));
        Assert.All(detalles, d => Assert.False(d.Devuelto));
    }

    [Fact]
    public async Task RegistrarPrestamo_SiFallaDentroDeLaTransaccion_RevierteLaDisponibilidadYaCambiada()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        var prestamoRepo = CreatePrestamoRepo(context);
        await equipoRepo.GuardarAsync(NuevoEquipo("EQ-TX"));
        // Un préstamo con el mismo ID que se intentará registrar ya existe -> GuardarAsync
        // fallará DESPUÉS de que la disponibilidad del equipo ya fue actualizada dentro
        // de la transacción, forzando el camino de rollback.
        await prestamoRepo.GuardarAsync(NuevoPrestamo("dup-id"));

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            service.RegistrarPrestamoAsync(NuevoPrestamo("dup-id"), new[] { "EQ-TX" }));

        Assert.Equal(ErrorCode.ErrorEscrituraBaseDatos, ex.Code);

        await using var verifyContext = CreateContext();
        var equipo = await verifyContext.Equipos.FindAsync("EQ-TX");
        Assert.True(equipo!.Disponible); // la transacción revirtió el cambio de disponibilidad
    }

    [Fact]
    public async Task RegistrarDevolucion_DeberiaActualizarEquiposADisponible_YMarcarPrestamoDevuelto()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        var prestamoRepo = CreatePrestamoRepo(context);

        await equipoRepo.GuardarAsync(NuevoEquipo("ET001", disponible: false));
        var prestamo = NuevoPrestamo("id-123");
        await prestamoRepo.GuardarAsync(prestamo);
        await prestamoRepo.GuardarDetalleAsync(new DetallePrestamo
        {
            Id = "det-1",
            PrestamoId = "id-123",
            EquipoCodigo = "ET001",
            ObservacionItem = "Estado al entregar: Bueno",
            Devuelto = false
        });

        var service = CreateService(context);
        await service.RegistrarDevolucionAsync("id-123", "Sin novedad");

        await using var verifyContext = CreateContext();
        var equipo = await verifyContext.Equipos.FindAsync("ET001");
        Assert.True(equipo!.Disponible);

        var prestamoActualizado = await verifyContext.Prestamos.FindAsync("id-123");
        Assert.Equal(EstadoPrestamo.Devuelto, prestamoActualizado!.Estado);
        Assert.NotNull(prestamoActualizado.FechaDevolucion);
        Assert.Contains("| Devolución: Sin novedad", prestamoActualizado.Observaciones);

        var detalle = await verifyContext.DetallesPrestamo.FindAsync("det-1");
        Assert.True(detalle!.Devuelto);
    }

    [Fact]
    public async Task RegistrarDevolucion_SinObservaciones_NoModificaLasObservacionesExistentes()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        var prestamoRepo = CreatePrestamoRepo(context);

        await equipoRepo.GuardarAsync(NuevoEquipo("ET002", disponible: false));
        var prestamo = NuevoPrestamo("id-456");
        prestamo.Observaciones = "Observación original";
        await prestamoRepo.GuardarAsync(prestamo);
        await prestamoRepo.GuardarDetalleAsync(new DetallePrestamo
        {
            Id = "det-2",
            PrestamoId = "id-456",
            EquipoCodigo = "ET002",
            Devuelto = false
        });

        var service = CreateService(context);
        await service.RegistrarDevolucionAsync("id-456", "   ");

        await using var verifyContext = CreateContext();
        var prestamoActualizado = await verifyContext.Prestamos.FindAsync("id-456");
        Assert.Equal("Observación original", prestamoActualizado!.Observaciones);
    }
}
