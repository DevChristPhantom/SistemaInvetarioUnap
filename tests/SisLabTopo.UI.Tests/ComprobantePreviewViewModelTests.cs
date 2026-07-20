using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.UI.Prestamos;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="ComprobantePreviewViewModel"/> que no dependen de rasterizar un
/// PDF real (eso se verificó visualmente, ver informe de la Fase 5): cubren que un fallo
/// al generar/renderizar el comprobante se refleje como <see cref="ComprobantePreviewViewModel.MensajeError"/>
/// en vez de propagar la excepción, y el comportamiento de <c>ImprimirCommand</c>.
/// </summary>
public class ComprobantePreviewViewModelTests
{
    private static Prestamo CrearPrestamo() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Docente = "Abdul Tacma Fernández",
        Curso = "Topografía Minera",
        Semestre = "2026-I",
        NombreEstudiante = "Estudiante de Prueba",
        CodigoEstudiante = "2020-001",
        FechaPrestamo = DateTime.Now,
    };

    [Fact]
    public async Task Cargar_CuandoFallaLaGeneracionDelPdf_MuestraMensajeDeErrorYNoPropagaLaExcepcion()
    {
        var generator = new Mock<IComprobantePrestamoGenerator>();
        generator.Setup(g => g.GenerarPdfAsync(
                It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<DetallePrestamo>>(), It.IsAny<IReadOnlyList<Equipo>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fallo simulado de generación de PDF."));

        var vm = new ComprobantePreviewViewModel(
            generator.Object, CrearPrestamo(), new List<DetallePrestamo>(), new List<Equipo>(), NullLogger.Instance);

        var excepcion = await Record.ExceptionAsync(() => vm.CargarAsync());

        Assert.Null(excepcion);
        Assert.Null(vm.VistaPrevia);
        Assert.False(string.IsNullOrEmpty(vm.MensajeError));
    }

    [Fact]
    public async Task Imprimir_ConExito_DisparaCierre()
    {
        var generator = new Mock<IComprobantePrestamoGenerator>();
        generator.Setup(g => g.ImprimirAsync(
                It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<DetallePrestamo>>(), It.IsAny<IReadOnlyList<Equipo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = new ComprobantePreviewViewModel(generator.Object, CrearPrestamo(), new List<DetallePrestamo>(), new List<Equipo>());
        var seCerro = false;
        vm.SolicitarCierre += (_, _) => seCerro = true;

        await vm.ImprimirCommand.ExecuteAsync(null);

        Assert.True(seCerro);
        Assert.True(string.IsNullOrEmpty(vm.MensajeError));
    }

    [Fact]
    public async Task Imprimir_CuandoFalla_MuestraMensajeDeErrorYNoDisparaCierre()
    {
        var generator = new Mock<IComprobantePrestamoGenerator>();
        generator.Setup(g => g.ImprimirAsync(
                It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<DetallePrestamo>>(), It.IsAny<IReadOnlyList<Equipo>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No hay impresora predeterminada."));

        var vm = new ComprobantePreviewViewModel(generator.Object, CrearPrestamo(), new List<DetallePrestamo>(), new List<Equipo>());
        var seCerro = false;
        vm.SolicitarCierre += (_, _) => seCerro = true;

        await vm.ImprimirCommand.ExecuteAsync(null);

        Assert.False(seCerro);
        Assert.Contains("No hay impresora", vm.MensajeError);
    }
}
