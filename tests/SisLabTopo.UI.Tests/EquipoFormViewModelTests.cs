using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;
using SisLabTopo.UI.Equipos;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="EquipoFormViewModel"/>: validación por campo (código y
/// denominación obligatorios), alta vs. edición, y que un <see cref="ServiceException"/>
/// del servicio se refleje como <see cref="EquipoFormViewModel.MensajeError"/> en vez de
/// propagarse.
/// </summary>
public class EquipoFormViewModelTests
{
    [Fact]
    public async Task Guardar_SinCodigoNiDenominacion_NoLlamaAlServicioYQuedaEnError()
    {
        var equipoService = new Mock<IEquipoService>();
        var vm = new EquipoFormViewModel(equipoService.Object, equipoExistente: null);

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.True(vm.HasErrors);
        Assert.True(vm.GetErrors(nameof(vm.Codigo)).Cast<object>().Any());
        Assert.True(vm.GetErrors(nameof(vm.Denominacion)).Cast<object>().Any());
        equipoService.Verify(s => s.RegistrarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(vm.GuardadoExitoso);
    }

    [Fact]
    public async Task Guardar_AltaNueva_MarcaDisponibleYRegistraFechaDeRegistro()
    {
        var equipoService = new Mock<IEquipoService>();
        Equipo? capturado = null;
        equipoService.Setup(s => s.RegistrarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()))
            .Callback<Equipo, CancellationToken>((eq, _) => capturado = eq)
            .Returns(Task.CompletedTask);

        var vm = new EquipoFormViewModel(equipoService.Object, equipoExistente: null)
        {
            Codigo = "EQ-100",
            Denominacion = "Nivel Topográfico",
        };

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.True(vm.GuardadoExitoso);
        Assert.NotNull(capturado);
        Assert.True(capturado!.Disponible);
        Assert.True(capturado.FechaRegistro > DateTime.MinValue);
        equipoService.Verify(s => s.ActualizarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Guardar_Edicion_LlamaActualizarNoRegistrar()
    {
        var equipoExistente = new Equipo { Codigo = "EQ-050", Denominacion = "GPS Diferencial", Estado = EstadoEquipo.Bueno };
        var equipoService = new Mock<IEquipoService>();
        equipoService.Setup(s => s.ActualizarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var vm = new EquipoFormViewModel(equipoService.Object, equipoExistente);

        Assert.True(vm.EsEdicion);
        Assert.Equal("EQ-050", vm.Codigo);

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.True(vm.GuardadoExitoso);
        equipoService.Verify(s => s.ActualizarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()), Times.Once);
        equipoService.Verify(s => s.RegistrarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Guardar_CuandoElServicioFalla_MuestraMensajeDeErrorYNoPropagaLaExcepcion()
    {
        var equipoService = new Mock<IEquipoService>();
        equipoService.Setup(s => s.RegistrarAsync(It.IsAny<Equipo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.DatosInvalidos, "El código patrimonial ya se encuentra registrado."));

        var vm = new EquipoFormViewModel(equipoService.Object, equipoExistente: null)
        {
            Codigo = "EQ-100",
            Denominacion = "Nivel Topográfico",
        };

        var excepcion = await Record.ExceptionAsync(() => vm.GuardarCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.False(vm.GuardadoExitoso);
        Assert.Contains("ya se encuentra registrado", vm.MensajeError);
    }
}
