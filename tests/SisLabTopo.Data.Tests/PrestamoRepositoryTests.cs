using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Tests;

/// <summary>
/// Espeja el espíritu de las pruebas de repositorio de préstamos en Java (que se
/// probaban indirectamente vía <c>PrestamoServiceImplTest</c> con un repositorio en
/// memoria) para <see cref="PrestamoRepository"/> contra SQLite real: préstamos,
/// detalles de préstamo y parámetros de configuración.
/// </summary>
public class PrestamoRepositoryTests : SqliteTestBase
{
    private static PrestamoRepository CreatePrestamoRepo(SisLabTopoDbContext context) =>
        new(context, NullLogger<PrestamoRepository>.Instance);

    private static EquipoRepository CreateEquipoRepo(SisLabTopoDbContext context) =>
        new(context, NullLogger<EquipoRepository>.Instance);

    private static Prestamo NuevoPrestamo(string id) => new()
    {
        Id = id,
        Docente = "Abdul Tacma Fernández",
        Curso = "Topografía I",
        Semestre = "2026-I",
        NombreEstudiante = "Juan Pérez",
        CodigoEstudiante = "2020-12345",
        FechaPrestamo = DateTime.Now,
        Estado = EstadoPrestamo.Activo,
        Observaciones = "Ninguna",
        FechaRegistro = DateTime.Now
    };

    private static Equipo NuevoEquipo(string codigo) => new()
    {
        Codigo = codigo,
        Denominacion = "Estación Total Topcon",
        Estado = EstadoEquipo.Bueno,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = true,
        FechaRegistro = DateTime.Now
    };

    [Fact]
    public async Task Guardar_DeberiaPermitirRecuperarElMismoPrestamo()
    {
        await using var context = CreateContext();
        var repo = CreatePrestamoRepo(context);

        await repo.GuardarAsync(NuevoPrestamo("PR-TEST-01"));

        var resultado = await repo.BuscarPorIdAsync("PR-TEST-01");

        Assert.NotNull(resultado);
        Assert.Equal("Juan Pérez", resultado!.NombreEstudiante);
        Assert.Equal(EstadoPrestamo.Activo, resultado.Estado);
    }

    [Fact]
    public async Task Guardar_ConIdDuplicado_DeberiaLanzarServiceException()
    {
        await using var context = CreateContext();
        var repo = CreatePrestamoRepo(context);

        await repo.GuardarAsync(NuevoPrestamo("PR-DUP-01"));

        var ex = await Assert.ThrowsAsync<ServiceException>(
            () => repo.GuardarAsync(NuevoPrestamo("PR-DUP-01")));

        Assert.Equal(ErrorCode.DatosInvalidos, ex.Code);
    }

    [Fact]
    public async Task Actualizar_DeberiaModificarEstadoYFechaDevolucion()
    {
        await using var context = CreateContext();
        var repo = CreatePrestamoRepo(context);

        var prestamo = NuevoPrestamo("PR-TEST-02");
        await repo.GuardarAsync(prestamo);

        prestamo.Estado = EstadoPrestamo.Devuelto;
        prestamo.FechaDevolucion = DateTime.Now;
        await repo.ActualizarAsync(prestamo);

        var resultado = await repo.BuscarPorIdAsync("PR-TEST-02");

        Assert.NotNull(resultado);
        Assert.Equal(EstadoPrestamo.Devuelto, resultado!.Estado);
        Assert.NotNull(resultado.FechaDevolucion);
    }

    [Fact]
    public async Task Actualizar_PrestamoInexistente_DeberiaLanzarServiceException()
    {
        await using var context = CreateContext();
        var repo = CreatePrestamoRepo(context);

        var ex = await Assert.ThrowsAsync<ServiceException>(
            () => repo.ActualizarAsync(NuevoPrestamo("NO-EXISTE")));

        Assert.Equal(ErrorCode.PrestamoNoEncontrado, ex.Code);
    }

    [Fact]
    public async Task GuardarDetalle_DeberiaAparecerEnObtenerDetalleYObtenerTodosDetalles()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        var prestamoRepo = CreatePrestamoRepo(context);

        await equipoRepo.GuardarAsync(NuevoEquipo("EQ-DET-01"));
        await prestamoRepo.GuardarAsync(NuevoPrestamo("PR-DET-01"));

        var detalle = new DetallePrestamo
        {
            Id = "DET-01",
            PrestamoId = "PR-DET-01",
            EquipoCodigo = "EQ-DET-01",
            ObservacionItem = "Sin observaciones",
            Devuelto = false
        };
        await prestamoRepo.GuardarDetalleAsync(detalle);

        var detallesDelPrestamo = await prestamoRepo.ObtenerDetalleAsync("PR-DET-01");
        var todosLosDetalles = await prestamoRepo.ObtenerTodosDetallesAsync();

        Assert.Single(detallesDelPrestamo);
        Assert.Equal("EQ-DET-01", detallesDelPrestamo[0].EquipoCodigo);
        Assert.Single(todosLosDetalles);
    }

    [Fact]
    public async Task ActualizarDetalle_DeberiaMarcarComoDevuelto()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        var prestamoRepo = CreatePrestamoRepo(context);

        await equipoRepo.GuardarAsync(NuevoEquipo("EQ-DET-02"));
        await prestamoRepo.GuardarAsync(NuevoPrestamo("PR-DET-02"));

        var detalle = new DetallePrestamo
        {
            Id = "DET-02",
            PrestamoId = "PR-DET-02",
            EquipoCodigo = "EQ-DET-02",
            Devuelto = false
        };
        await prestamoRepo.GuardarDetalleAsync(detalle);

        detalle.Devuelto = true;
        await prestamoRepo.ActualizarDetalleAsync(detalle);

        var resultado = (await prestamoRepo.ObtenerDetalleAsync("PR-DET-02")).Single();
        Assert.True(resultado.Devuelto);
    }

    [Fact]
    public async Task ObtenerConfig_ParaClaveInexistente_DeberiaDevolverCadenaVacia()
    {
        await using var context = CreateContext();
        var repo = CreatePrestamoRepo(context);

        var valor = await repo.ObtenerConfigAsync("clave.que.no.existe");

        Assert.Equal(string.Empty, valor);
    }

    [Fact]
    public async Task GuardarConfig_DeberiaInsertarYLuegoActualizarLaMismaClave()
    {
        await using var context = CreateContext();
        var repo = CreatePrestamoRepo(context);

        await repo.GuardarConfigAsync("institucion.nombre", "Universidad Nacional del Altiplano");
        var primeraLectura = await repo.ObtenerConfigAsync("institucion.nombre");

        await repo.GuardarConfigAsync("institucion.nombre", "UNA Puno");
        var segundaLectura = await repo.ObtenerConfigAsync("institucion.nombre");

        Assert.Equal("Universidad Nacional del Altiplano", primeraLectura);
        Assert.Equal("UNA Puno", segundaLectura);
    }
}
