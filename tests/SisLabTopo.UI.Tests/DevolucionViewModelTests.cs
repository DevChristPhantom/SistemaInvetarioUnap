using Moq;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Prestamos;

namespace SisLabTopo.UI.Tests;

/// <summary>Pruebas de <see cref="DevolucionViewModel"/>: puerto funcional de <c>DevolucionDialog.java</c>.</summary>
public class DevolucionViewModelTests
{
    [Fact]
    public async Task Registrar_ConExito_MarcaDevueltoExitosoYDisparaCierre()
    {
        var prestamoService = new Mock<IPrestamoService>();
        prestamoService.Setup(s => s.RegistrarDevolucionAsync("prestamo-1", "Sin novedad", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = new DevolucionViewModel(prestamoService.Object, "prestamo-1") { Observaciones = "Sin novedad" };
        var seCerro = false;
        vm.SolicitarCierre += (_, _) => seCerro = true;

        await vm.RegistrarCommand.ExecuteAsync(null);

        Assert.True(vm.DevueltoExitoso);
        Assert.True(seCerro);
    }

    [Fact]
    public async Task Registrar_CuandoElServicioFalla_MuestraMensajeDeErrorYNoPropagaLaExcepcion()
    {
        var prestamoService = new Mock<IPrestamoService>();
        prestamoService.Setup(s => s.RegistrarDevolucionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.PrestamoYaDevuelto, "El préstamo ya fue devuelto con anterioridad."));

        var vm = new DevolucionViewModel(prestamoService.Object, "prestamo-1");

        var excepcion = await Record.ExceptionAsync(() => vm.RegistrarCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.False(vm.DevueltoExitoso);
        Assert.Contains("ya fue devuelto", vm.MensajeError);
    }
}
