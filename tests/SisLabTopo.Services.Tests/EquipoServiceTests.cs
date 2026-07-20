using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Puerto 1:1 de <c>EquipoServiceTest.java</c> (4 pruebas), usando Moq sobre
/// <see cref="IEquipoRepository"/> igual que la versión Java usa Mockito sobre
/// <c>EquipoRepository</c>. Se usa el validador REAL (<see cref="EquipoValidator"/>,
/// no un mock) para que la prueba de "campos obligatorios vacíos" ejercite la
/// validación de verdad, igual que Java ejercita Bean Validation de verdad.
/// </summary>
public class EquipoServiceTests
{
    private readonly Mock<IEquipoRepository> _repository = new();
    private readonly EquipoService _service;

    public EquipoServiceTests()
    {
        _service = new EquipoService(_repository.Object, new EquipoValidator(), NullLogger<EquipoService>.Instance);
    }

    private static Equipo CrearEquipo(string codigo = "EQ-01", bool disponible = true) => new()
    {
        Codigo = codigo,
        Denominacion = "Estación Total",
        Modelo = "GTS",
        Marca = "Topcon",
        Serie = "999",
        Estado = EstadoEquipo.Bueno,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = disponible,
        Observacion = "",
        FechaRegistro = DateTime.Now
    };

    [Fact]
    public async Task Registrar_DeberiaGuardarEquipo_CuandoEsValido()
    {
        var equipo = CrearEquipo();
        _repository.Setup(r => r.BuscarPorCodigoAsync("EQ-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Equipo?)null);

        await _service.RegistrarAsync(equipo);

        _repository.Verify(r => r.GuardarAsync(equipo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Registrar_DeberiaFallar_CuandoCodigoYaExiste()
    {
        var equipo = CrearEquipo();
        _repository.Setup(r => r.BuscarPorCodigoAsync("EQ-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(equipo);

        await Assert.ThrowsAsync<ServiceException>(() => _service.RegistrarAsync(equipo));

        _repository.Verify(r => r.GuardarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Registrar_DeberiaFallar_CuandoCamposObligatoriosEstanVacios()
    {
        var equipo = CrearEquipo();
        equipo.Codigo = "";
        equipo.Denominacion = "";

        await Assert.ThrowsAsync<ServiceException>(() => _service.RegistrarAsync(equipo));
    }

    [Fact]
    public async Task Eliminar_DeberiaFallar_CuandoEquipoNoEstaDisponible()
    {
        var equipo = CrearEquipo(disponible: false); // prestado
        _repository.Setup(r => r.BuscarPorCodigoAsync("EQ-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(equipo);

        await Assert.ThrowsAsync<ServiceException>(() => _service.EliminarAsync("EQ-01"));

        _repository.Verify(r => r.EliminarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Actualizar_DeberiaFallar_CuandoEquipoNoExiste()
    {
        var equipo = CrearEquipo();
        _repository.Setup(r => r.BuscarPorCodigoAsync("EQ-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Equipo?)null);

        var ex = await Assert.ThrowsAsync<ServiceException>(() => _service.ActualizarAsync(equipo));

        Assert.Equal(ErrorCode.EquipoNoEncontrado, ex.Code);
        _repository.Verify(r => r.ActualizarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObtenerTodos_CuandoRepositorioFalla_DevuelveListaVacia()
    {
        _repository.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.ErrorLecturaBaseDatos, "boom"));

        var resultado = await _service.ObtenerTodosAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task BuscarPorTexto_DeberiaFiltrarPorSubcadenaSinDistinguirMayusculas()
    {
        var equipos = new List<Equipo>
        {
            CrearEquipo("EQ-01"),
            new()
            {
                Codigo = "EQ-02", Denominacion = "Nivel Automático", Marca = "Leica", Modelo = "NA2",
                Serie = "111", Estado = EstadoEquipo.Bueno, Tipo = TipoEquipo.NivelTopografico,
                Disponible = true, FechaRegistro = DateTime.Now
            }
        };
        _repository.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(equipos);

        var resultado = await _service.BuscarPorTextoAsync("leica");

        Assert.Single(resultado);
        Assert.Equal("EQ-02", resultado[0].Codigo);
    }
}
