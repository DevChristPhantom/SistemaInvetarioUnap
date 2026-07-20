using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Prestamos;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="PrestamosViewModel"/>: "Ver Detalle"/"Registrar Devolución"/
/// "Imprimir" deshabilitados sin selección, y que el conteo de equipos por préstamo se
/// resuelva sin depender de que la vista repinte celdas (ver comentario de
/// <c>PrestamosViewModel.CargarAsync</c> sobre el problema N+1 original de Java).
/// </summary>
public class PrestamosViewModelTests
{
    private static Prestamo CrearPrestamo(string id, DateTime fechaPrestamo) => new()
    {
        Id = id,
        Docente = "Abdul Tacma Fernández",
        Curso = "Topografía Minera",
        Semestre = "2026-I",
        NombreEstudiante = "Estudiante de Prueba",
        CodigoEstudiante = "2020-001",
        FechaPrestamo = fechaPrestamo,
        Estado = EstadoPrestamo.Activo,
        FechaRegistro = fechaPrestamo,
    };

    private static PrestamosViewModel CrearViewModel(
        out Mock<IPrestamoService> prestamoService,
        out Mock<IEquipoService> equipoService,
        out Mock<IComprobantePrestamoGenerator> comprobanteGenerator,
        out Mock<IDialogService> dialogService)
    {
        prestamoService = new Mock<IPrestamoService>();
        equipoService = new Mock<IEquipoService>();
        comprobanteGenerator = new Mock<IComprobantePrestamoGenerator>();
        dialogService = new Mock<IDialogService>();
        return new PrestamosViewModel(
            prestamoService.Object, equipoService.Object, comprobanteGenerator.Object, dialogService.Object,
            NullLogger<PrestamosViewModel>.Instance);
    }

    [Fact]
    public void SinSeleccion_AccionesQuedanDeshabilitadas()
    {
        var vm = CrearViewModel(out _, out _, out _, out _);

        Assert.False(vm.VerDetalleCommand.CanExecute(null));
        Assert.False(vm.RegistrarDevolucionCommand.CanExecute(null));
        Assert.False(vm.ImprimirCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cargar_CalculaCantidadDeEquiposUnaSolaVezPorPrestamo()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _, out _);
        var p1 = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now);
        var p2 = CrearPrestamo("22222222-aaaa-bbbb-cccc-000000000002", DateTime.Now);

        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo> { p1, p2 });
        prestamoService.Setup(s => s.ObtenerDetalleAsync(p1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo> { new(), new() });
        prestamoService.Setup(s => s.ObtenerDetalleAsync(p2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo> { new() });

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Prestamos.Count);
        Assert.Equal(2, vm.Prestamos.Single(p => p.Prestamo.Id == p1.Id).CantidadEquipos);
        Assert.Equal(1, vm.Prestamos.Single(p => p.Prestamo.Id == p2.Id).CantidadEquipos);

        // Exactamente una llamada de detalle por préstamo activo -- nunca más de una por
        // recarga (la vista jamás debe volver a invocar el servicio desde el
        // renderizado de una celda).
        prestamoService.Verify(s => s.ObtenerDetalleAsync(p1.Id, It.IsAny<CancellationToken>()), Times.Once);
        prestamoService.Verify(s => s.ObtenerDetalleAsync(p2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IdCorto_TomaSoloLosPrimerosOchoCaracteres()
    {
        var prestamo = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now);
        var fila = new PrestamoRowViewModel(prestamo, 0);

        Assert.Equal("11111111", fila.IdCorto);
    }

    [Fact]
    public async Task ConSeleccion_RegistrarDevolucion_AbreElDialogoYRecargaSiHuboExito()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _, out var dialogService);
        var prestamo = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now);
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo> { prestamo });
        prestamoService.Setup(s => s.ObtenerDetalleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<DetallePrestamo>());
        await vm.CargarCommand.ExecuteAsync(null);

        vm.PrestamoSeleccionado = vm.Prestamos.Single();
        Assert.True(vm.RegistrarDevolucionCommand.CanExecute(null));

        prestamoService.Setup(s => s.RegistrarDevolucionAsync(prestamo.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Simula que, dentro del diálogo modal (ShowDialog bloquea hasta que se
        // cierra), el usuario confirmó la devolución con éxito antes de que
        // ShowDialog "regrese" -- así se puede comprobar que PrestamosViewModel
        // reacciona a DevueltoExitoso sin necesitar una ventana WPF real.
        DevolucionViewModel? dialogoAbierto = null;
        dialogService.Setup(d => d.ShowDialog(It.IsAny<DevolucionViewModel>()))
            .Returns((DevolucionViewModel vmDialogo) =>
            {
                dialogoAbierto = vmDialogo;
                vmDialogo.RegistrarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                return true;
            });

        await vm.RegistrarDevolucionCommand.ExecuteAsync(null);

        Assert.NotNull(dialogoAbierto);
        // Se recarga la tabla otra vez tras el diálogo (segunda llamada a ObtenerActivosAsync).
        prestamoService.Verify(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
