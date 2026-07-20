using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Tests;

/// <summary>
/// Espeja el espíritu de <c>ExcelEquipoRepositoryTest.java</c>: guardar→recuperar,
/// actualizar, eliminar, actualizar-solo-un-campo — ahora contra SQLite real.
/// </summary>
public class EquipoRepositoryTests : SqliteTestBase
{
    private static EquipoRepository CreateRepo(SisLabTopoDbContext context) =>
        new(context, NullLogger<EquipoRepository>.Instance);

    private static Equipo NuevoEquipo(string codigo = "EQ-TEST-01") => new()
    {
        Codigo = codigo,
        Denominacion = "Estación Total Topcon",
        Modelo = "GTS-230",
        Marca = "Topcon",
        Serie = "123456",
        Estado = EstadoEquipo.Bueno,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = true,
        Observacion = "Ninguna",
        FechaRegistro = DateTime.Now
    };

    [Fact]
    public async Task Guardar_DeberiaPermitirRecuperarElMismoEquipo()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        await repo.GuardarAsync(NuevoEquipo());

        var resultado = await repo.BuscarPorCodigoAsync("EQ-TEST-01");

        Assert.NotNull(resultado);
        Assert.Equal("Estación Total Topcon", resultado!.Denominacion);
        Assert.Equal("Topcon", resultado.Marca);
        Assert.Equal(EstadoEquipo.Bueno, resultado.Estado);
        Assert.True(resultado.Disponible);
    }

    [Fact]
    public async Task Guardar_ConCodigoDuplicado_DeberiaLanzarServiceException()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        await repo.GuardarAsync(NuevoEquipo("EQ-DUP-01"));

        var ex = await Assert.ThrowsAsync<ServiceException>(
            () => repo.GuardarAsync(NuevoEquipo("EQ-DUP-01")));

        Assert.Equal(ErrorCode.DatosInvalidos, ex.Code);
    }

    [Fact]
    public async Task Actualizar_DeberiaModificarValoresExistentes()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        var equipo = NuevoEquipo("EQ-TEST-02");
        equipo.Denominacion = "Teodolito Leica";
        equipo.Estado = EstadoEquipo.Nuevo;
        equipo.Tipo = TipoEquipo.Teodolito;
        await repo.GuardarAsync(equipo);

        equipo.Denominacion = "Teodolito Leica Modificado";
        equipo.Estado = EstadoEquipo.Regular;
        await repo.ActualizarAsync(equipo);

        var resultado = await repo.BuscarPorCodigoAsync("EQ-TEST-02");

        Assert.NotNull(resultado);
        Assert.Equal("Teodolito Leica Modificado", resultado!.Denominacion);
        Assert.Equal(EstadoEquipo.Regular, resultado.Estado);
    }

    [Fact]
    public async Task Actualizar_EquipoInexistente_DeberiaLanzarServiceException()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        var ex = await Assert.ThrowsAsync<ServiceException>(
            () => repo.ActualizarAsync(NuevoEquipo("EQ-NO-EXISTE")));

        Assert.Equal(ErrorCode.EquipoNoEncontrado, ex.Code);
    }

    [Fact]
    public async Task Eliminar_DeberiaRemoverEquipoCorrectamente()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        await repo.GuardarAsync(NuevoEquipo("EQ-TEST-03"));
        var antes = await repo.BuscarPorCodigoAsync("EQ-TEST-03");
        Assert.NotNull(antes);

        await repo.EliminarAsync("EQ-TEST-03");

        var despues = await repo.BuscarPorCodigoAsync("EQ-TEST-03");
        Assert.Null(despues);
    }

    [Fact]
    public async Task Eliminar_EquipoInexistente_DeberiaLanzarServiceException()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        var ex = await Assert.ThrowsAsync<ServiceException>(() => repo.EliminarAsync("NO-EXISTE"));

        Assert.Equal(ErrorCode.EquipoNoEncontrado, ex.Code);
    }

    [Fact]
    public async Task ActualizarDisponibilidad_DeberiaCambiarSoloEseCampo()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        var equipo = NuevoEquipo("EQ-TEST-04");
        equipo.Denominacion = "Nivel Automático";
        equipo.Tipo = TipoEquipo.NivelTopografico;
        await repo.GuardarAsync(equipo);

        await repo.ActualizarDisponibilidadAsync("EQ-TEST-04", false);

        var resultado = await repo.BuscarPorCodigoAsync("EQ-TEST-04");

        Assert.NotNull(resultado);
        Assert.False(resultado!.Disponible);
        Assert.Equal("Nivel Automático", resultado.Denominacion); // otros campos no cambiaron
    }

    [Fact]
    public async Task BuscarPorCodigo_EsInsensibleAMayusculasYMinusculas()
    {
        await using var context = CreateContext();
        var repo = CreateRepo(context);

        await repo.GuardarAsync(NuevoEquipo("EQ-CASE-01"));

        var resultado = await repo.BuscarPorCodigoAsync("eq-case-01");

        Assert.NotNull(resultado);
        Assert.Equal("EQ-CASE-01", resultado!.Codigo);
    }

    [Fact]
    public async Task ObtenerTodos_DeberiaReflejarLoGuardadoEnOtroContexto()
    {
        // Escritura y lectura con DbContexts distintos, para verificar que la
        // persistencia real llega al archivo SQLite (no depende de una caché en
        // memoria compartida por instancia, a diferencia del bug de caché por
        // lastModified() de la versión Excel en Java).
        await using (var writeContext = CreateContext())
        {
            var writeRepo = CreateRepo(writeContext);
            await writeRepo.GuardarAsync(NuevoEquipo("EQ-A"));
            await writeRepo.GuardarAsync(NuevoEquipo("EQ-B"));
        }

        await using var readContext = CreateContext();
        var readRepo = CreateRepo(readContext);
        var todos = await readRepo.ObtenerTodosAsync();

        Assert.Equal(2, todos.Count);
    }
}
