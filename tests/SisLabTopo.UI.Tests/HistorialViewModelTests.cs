using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Historial;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="HistorialViewModel"/>: filtros de rango de fechas/Estado/
/// Semestre (aplicados sobre los datos ya traídos, igual que <c>HistorialPanel.aplicarFiltros()</c>
/// en Java, pero sin el riesgo de fecha mal formateada porque aquí son
/// <see cref="DateTime"/>? reales en vez de texto libre) y el conteo de equipos por
/// préstamo resuelto sin N+1 (una sola pasada en paralelo por recarga).
/// </summary>
public class HistorialViewModelTests
{
    private static Prestamo CrearPrestamo(string id, DateTime fechaPrestamo, EstadoPrestamo estado = EstadoPrestamo.Activo, string semestre = "2026-I") => new()
    {
        Id = id,
        Docente = "Abdul Tacma Fernández",
        Curso = "Topografía Minera",
        Semestre = semestre,
        NombreEstudiante = "Estudiante de Prueba",
        CodigoEstudiante = "2020-001",
        FechaPrestamo = fechaPrestamo,
        Estado = estado,
        FechaRegistro = fechaPrestamo,
    };

    private static HistorialViewModel CrearViewModel(
        out Mock<IPrestamoService> prestamoService,
        out Mock<IReporteService> reporteService,
        out Mock<IDialogService> dialogService)
    {
        prestamoService = new Mock<IPrestamoService>();
        reporteService = new Mock<IReporteService>();
        dialogService = new Mock<IDialogService>();
        prestamoService.Setup(s => s.ObtenerDetalleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo>());
        return new HistorialViewModel(prestamoService.Object, reporteService.Object, dialogService.Object, NullLogger<HistorialViewModel>.Instance);
    }

    [Fact]
    public async Task Cargar_ReiniciaFiltrosATodosYTraeTodosLosPrestamos()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _);
        prestamoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prestamo> { CrearPrestamo("1", DateTime.Now), CrearPrestamo("2", DateTime.Now) });

        vm.EstadoSeleccionado = vm.OpcionesEstado.Single(o => o.Valor == EstadoPrestamo.Devuelto);
        vm.SemestreSeleccionado = "2027-I";

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Null(vm.EstadoSeleccionado.Valor);
        Assert.Equal("Todos", vm.SemestreSeleccionado);
        Assert.Equal(2, vm.Prestamos.Count);
    }

    [Fact]
    public async Task FiltrarPorEstado_SoloDejaLosPrestamosConEseEstado()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _);
        var activo = CrearPrestamo("1", DateTime.Now, EstadoPrestamo.Activo);
        var devuelto = CrearPrestamo("2", DateTime.Now, EstadoPrestamo.Devuelto);
        prestamoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prestamo> { activo, devuelto });
        await vm.CargarCommand.ExecuteAsync(null);

        vm.EstadoSeleccionado = vm.OpcionesEstado.Single(o => o.Valor == EstadoPrestamo.Devuelto);
        await vm.FiltrarCommand.ExecuteAsync(null);

        Assert.Single(vm.Prestamos);
        Assert.Equal(devuelto.Id, vm.Prestamos[0].Prestamo.Id);
    }

    [Fact]
    public async Task FiltrarPorSemestre_SoloDejaLosPrestamosDeEseSemestre()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _);
        var semestre1 = CrearPrestamo("1", DateTime.Now, semestre: "2026-I");
        var semestre2 = CrearPrestamo("2", DateTime.Now, semestre: "2027-II");
        prestamoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prestamo> { semestre1, semestre2 });
        await vm.CargarCommand.ExecuteAsync(null);

        vm.SemestreSeleccionado = "2027-II";
        await vm.FiltrarCommand.ExecuteAsync(null);

        Assert.Single(vm.Prestamos);
        Assert.Equal(semestre2.Id, vm.Prestamos[0].Prestamo.Id);
    }

    [Fact]
    public async Task FiltrarPorRangoDeFechas_ExcluyeLosPrestamosFueraDelRango()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _);
        var dentro = CrearPrestamo("1", DateTime.Today);
        var fuera = CrearPrestamo("2", DateTime.Today.AddYears(-2));
        prestamoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prestamo> { dentro, fuera });
        await vm.CargarCommand.ExecuteAsync(null);

        vm.FechaDesde = DateTime.Today.AddDays(-1);
        vm.FechaHasta = DateTime.Today.AddDays(1);
        await vm.FiltrarCommand.ExecuteAsync(null);

        Assert.Single(vm.Prestamos);
        Assert.Equal(dentro.Id, vm.Prestamos[0].Prestamo.Id);
    }

    [Fact]
    public async Task Cargar_CalculaCantidadDeEquiposUnaSolaVezPorPrestamo()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _);
        var p1 = CrearPrestamo("1", DateTime.Now);
        prestamoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo> { p1 });
        prestamoService.Setup(s => s.ObtenerDetalleAsync(p1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo> { new(), new(), new() });

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Prestamos.Single().CantidadEquipos);
        prestamoService.Verify(s => s.ObtenerDetalleAsync(p1.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportarExcel_SinFechas_MuestraErrorYNoLlamaAlServicio()
    {
        var vm = CrearViewModel(out _, out var reporteService, out var dialogService);
        vm.FechaDesde = null;

        await vm.ExportarExcelCommand.ExecuteAsync(null);

        dialogService.Verify(d => d.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        reporteService.Verify(r => r.ExportarHistorialExcelAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportarExcel_SinArchivoElegido_NoLlamaAlServicio()
    {
        var vm = CrearViewModel(out _, out var reporteService, out var dialogService);
        dialogService.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns((string?)null);

        await vm.ExportarExcelCommand.ExecuteAsync(null);

        reporteService.Verify(r => r.ExportarHistorialExcelAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportarExcel_ConFechasYArchivoElegido_CopiaElArchivoGeneradoAlDestino()
    {
        var vm = CrearViewModel(out _, out var reporteService, out var dialogService);
        var origen = Path.GetTempFileName();
        await File.WriteAllTextAsync(origen, "contenido-de-prueba");
        var destino = Path.Combine(Path.GetTempPath(), $"historial_{Guid.NewGuid():N}.xlsx");

        try
        {
            dialogService.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(destino);
            reporteService.Setup(r => r.ExportarHistorialExcelAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(origen);

            await vm.ExportarExcelCommand.ExecuteAsync(null);

            Assert.True(File.Exists(destino));
        }
        finally
        {
            File.Delete(origen);
            File.Delete(destino);
        }
    }
}
