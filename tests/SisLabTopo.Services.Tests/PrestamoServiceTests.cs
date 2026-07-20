using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Data;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Puerto (parcial) de <c>PrestamoServiceTest.java</c> usando Moq, para los escenarios
/// que no necesitan ejercitar una transacción real (fallan durante la pre-validación,
/// antes de que <see cref="PrestamoService.RegistrarPrestamoAsync"/> abra la
/// transacción). El escenario "registrarDevolucion_deberiaActualizarEquiposADisponible"
/// de Java, y el flujo feliz de "registrarPrestamo", se portan como pruebas de
/// integración contra SQLite real en <see cref="PrestamoServiceIntegrationTests"/> —
/// ahí es donde vive la mejora de atomicidad real (transacción EF Core) que reemplaza
/// el "rollback manual" que hacía Java, así que mockear ocultaría justo lo que hay que
/// verificar.
/// </summary>
public class PrestamoServiceTests
{
    private readonly Mock<IPrestamoRepository> _prestamoRepo = new();
    private readonly Mock<IEquipoRepository> _equipoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly PrestamoService _service;

    public PrestamoServiceTests()
    {
        _service = new PrestamoService(
            _prestamoRepo.Object,
            _equipoRepo.Object,
            _unitOfWork.Object,
            new PrestamoValidator(),
            NullLogger<PrestamoService>.Instance);
    }

    private static Equipo CrearEquipo(string codigo, bool disponible) => new()
    {
        Codigo = codigo,
        Denominacion = "Equipo " + codigo,
        Modelo = "Modelo",
        Marca = "Marca",
        Serie = "Serie",
        Estado = EstadoEquipo.Bueno,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = disponible,
        Observacion = "",
        FechaRegistro = DateTime.Now
    };

    private static Prestamo CrearPrestamo() => new()
    {
        Id = "p-123",
        Docente = "Abdul Tacma",
        Curso = "Topografía Minera",
        Semestre = "2026-I",
        NombreEstudiante = "Juan Perez",
        CodigoEstudiante = "160244",
        FechaPrestamo = DateTime.Now,
        FechaDevolucion = null,
        Estado = EstadoPrestamo.Activo,
        Observaciones = "",
        FechaRegistro = DateTime.Now
    };

    [Fact]
    public async Task RegistrarPrestamo_DeberiaFallar_SiEquipoNoDisponible()
    {
        var equipo = CrearEquipo("ET001", disponible: false); // no disponible
        _equipoRepo.Setup(r => r.BuscarPorCodigoAsync("ET001", It.IsAny<CancellationToken>())).ReturnsAsync(equipo);

        await Assert.ThrowsAsync<ServiceException>(() =>
            _service.RegistrarPrestamoAsync(CrearPrestamo(), new[] { "ET001" }));

        _prestamoRepo.Verify(r => r.GuardarAsync(It.IsAny<Prestamo>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarPrestamo_DeberiaFallar_SiEquipoNoExiste()
    {
        _equipoRepo.Setup(r => r.BuscarPorCodigoAsync("NOEXISTE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Equipo?)null);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            _service.RegistrarPrestamoAsync(CrearPrestamo(), new[] { "NOEXISTE" }));

        Assert.Equal(ErrorCode.EquipoNoEncontrado, ex.Code);
        _prestamoRepo.Verify(r => r.GuardarAsync(It.IsAny<Prestamo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarPrestamo_DeberiaFallar_SinCodigosDeEquipo()
    {
        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            _service.RegistrarPrestamoAsync(CrearPrestamo(), Array.Empty<string>()));

        Assert.Equal(ErrorCode.DatosInvalidos, ex.Code);
        _equipoRepo.Verify(r => r.BuscarPorCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarDevolucion_DeberiaFallar_CuandoPrestamoNoExiste()
    {
        _prestamoRepo.Setup(r => r.BuscarPorIdAsync("id-inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prestamo?)null);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            _service.RegistrarDevolucionAsync("id-inexistente", null));

        Assert.Equal(ErrorCode.PrestamoNoEncontrado, ex.Code);
    }

    [Fact]
    public async Task RegistrarDevolucion_DeberiaFallar_CuandoYaFueDevuelto()
    {
        var prestamo = CrearPrestamo();
        prestamo.Estado = EstadoPrestamo.Devuelto;
        _prestamoRepo.Setup(r => r.BuscarPorIdAsync(prestamo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prestamo);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            _service.RegistrarDevolucionAsync(prestamo.Id, null));

        Assert.Equal(ErrorCode.PrestamoYaDevuelto, ex.Code);
    }

    [Fact]
    public async Task EquiposMasPrestados_DeberiaOrdenarDescendenteYRespetarElTope()
    {
        var detalles = new List<DetallePrestamo>
        {
            new() { Id = "1", PrestamoId = "p1", EquipoCodigo = "A", Devuelto = false },
            new() { Id = "2", PrestamoId = "p1", EquipoCodigo = "B", Devuelto = false },
            new() { Id = "3", PrestamoId = "p2", EquipoCodigo = "A", Devuelto = false },
            new() { Id = "4", PrestamoId = "p2", EquipoCodigo = "A", Devuelto = false },
            new() { Id = "5", PrestamoId = "p2", EquipoCodigo = "C", Devuelto = false }
        };
        _prestamoRepo.Setup(r => r.ObtenerTodosDetallesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(detalles);

        var resultado = await _service.EquiposMasPrestadosAsync(2);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("A", resultado[0].CodigoEquipo);
        Assert.Equal(3, resultado[0].Cantidad);
        Assert.Equal("B", resultado[1].CodigoEquipo);
        Assert.Equal(1, resultado[1].Cantidad);
    }

    [Fact]
    public async Task PrestamosPorMes_DeberiaRellenarConCeroLosMesesSinDatos()
    {
        var ahora = DateTime.Now;
        var prestamos = new List<Prestamo>();
        prestamos.Add(new Prestamo
        {
            Id = "p1",
            Docente = "D",
            Curso = "C",
            Semestre = "S",
            NombreEstudiante = "E",
            CodigoEstudiante = "1",
            FechaPrestamo = ahora, // mes actual: 1 préstamo
            Estado = EstadoPrestamo.Activo,
            FechaRegistro = ahora
        });
        _prestamoRepo.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(prestamos);

        var resultado = await _service.PrestamosPorMesAsync(6);

        Assert.Equal(6, resultado.Count);
        // Los primeros 5 meses (más antiguos) no tienen datos -> cantidad 0.
        Assert.All(resultado.Take(5), item => Assert.Equal(0, item.Cantidad));
        // El último elemento es el mes actual, con el único préstamo sembrado.
        var ultimo = resultado[^1];
        Assert.Equal(ahora.Year, ultimo.Anio);
        Assert.Equal(ahora.Month, ultimo.Mes);
        Assert.Equal(1, ultimo.Cantidad);
    }

    [Fact]
    public async Task ContarDevueltosHoy_SoloCuentaLosDevueltosConFechaDeHoy()
    {
        var hoy = DateTime.Now;
        var ayer = hoy.AddDays(-1);
        var prestamos = new List<Prestamo>
        {
            new()
            {
                Id = "p1", Docente = "D", Curso = "C", Semestre = "S", NombreEstudiante = "E", CodigoEstudiante = "1",
                FechaPrestamo = ayer, FechaDevolucion = hoy, Estado = EstadoPrestamo.Devuelto, FechaRegistro = ayer
            },
            new()
            {
                Id = "p2", Docente = "D", Curso = "C", Semestre = "S", NombreEstudiante = "E", CodigoEstudiante = "1",
                FechaPrestamo = ayer, FechaDevolucion = ayer, Estado = EstadoPrestamo.Devuelto, FechaRegistro = ayer
            },
            new()
            {
                Id = "p3", Docente = "D", Curso = "C", Semestre = "S", NombreEstudiante = "E", CodigoEstudiante = "1",
                FechaPrestamo = ayer, FechaDevolucion = null, Estado = EstadoPrestamo.Activo, FechaRegistro = ayer
            }
        };
        _prestamoRepo.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(prestamos);

        var resultado = await _service.ContarDevueltosHoyAsync();

        Assert.Equal(1, resultado);
    }
}
