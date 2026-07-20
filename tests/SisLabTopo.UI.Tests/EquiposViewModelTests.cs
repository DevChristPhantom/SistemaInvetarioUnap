using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Equipos;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="EquiposViewModel"/>: reglas de negocio expuestas en la UI que
/// pide el plan de Fase 5 -- "Editar"/"Eliminar" deshabilitados sin selección,
/// confirmación antes de eliminar, y que los errores del servicio se reflejen en el
/// ViewModel (nunca que la excepción se propague sin manejar).
/// </summary>
public class EquiposViewModelTests
{
    private static Equipo CrearEquipo(string codigo = "EQ-001") => new()
    {
        Codigo = codigo,
        Denominacion = "Estación Total",
        Estado = EstadoEquipo.Bueno,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = true,
    };

    private static EquiposViewModel CrearViewModel(
        out Mock<IEquipoService> equipoService,
        out Mock<IReporteService> reporteService,
        out Mock<IDialogService> dialogService)
    {
        equipoService = new Mock<IEquipoService>();
        reporteService = new Mock<IReporteService>();
        dialogService = new Mock<IDialogService>();
        return new EquiposViewModel(equipoService.Object, reporteService.Object, dialogService.Object, NullLogger<EquiposViewModel>.Instance);
    }

    [Fact]
    public async Task Cargar_TraeTodosLosEquiposYReiniciaFiltros()
    {
        var vm = CrearViewModel(out var equipoService, out _, out _);
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { CrearEquipo("EQ-001"), CrearEquipo("EQ-002") });

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Equipos.Count);
        Assert.Null(vm.EstadoSeleccionado.Valor);
        Assert.Null(vm.TipoSeleccionado.Valor);
    }

    [Fact]
    public async Task FiltrarPorEstado_AplicaSoloSobreElCacheLocal()
    {
        var vm = CrearViewModel(out var equipoService, out _, out _);
        var bueno = CrearEquipo("EQ-001");
        bueno.Estado = EstadoEquipo.Bueno;
        var malo = CrearEquipo("EQ-002");
        malo.Estado = EstadoEquipo.Malo;
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo> { bueno, malo });

        await vm.CargarCommand.ExecuteAsync(null);
        vm.EstadoSeleccionado = vm.OpcionesEstado.Single(o => o.Valor == EstadoEquipo.Malo);

        Assert.Single(vm.Equipos);
        Assert.Equal("EQ-002", vm.Equipos[0].Codigo);
    }

    [Fact]
    public void SinSeleccion_EditarYEliminarQuedanDeshabilitados()
    {
        var vm = CrearViewModel(out _, out _, out _);

        Assert.False(vm.EditarCommand.CanExecute(null));
        Assert.False(vm.EliminarCommand.CanExecute(null));
    }

    [Fact]
    public void ConSeleccion_EditarYEliminarQuedanHabilitados()
    {
        var vm = CrearViewModel(out _, out _, out _);
        vm.EquipoSeleccionado = CrearEquipo();

        Assert.True(vm.EditarCommand.CanExecute(null));
        Assert.True(vm.EliminarCommand.CanExecute(null));
    }

    [Fact]
    public async Task Eliminar_SinConfirmacion_NoLlamaAlServicio()
    {
        var vm = CrearViewModel(out var equipoService, out _, out var dialogService);
        vm.EquipoSeleccionado = CrearEquipo();
        dialogService.Setup(d => d.ShowConfirm(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await vm.EliminarCommand.ExecuteAsync(null);

        equipoService.Verify(s => s.EliminarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Eliminar_ConConfirmacion_LlamaAlServicioYRecarga()
    {
        var vm = CrearViewModel(out var equipoService, out _, out var dialogService);
        vm.EquipoSeleccionado = CrearEquipo("EQ-001");
        dialogService.Setup(d => d.ShowConfirm(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo>());

        await vm.EliminarCommand.ExecuteAsync(null);

        equipoService.Verify(s => s.EliminarAsync("EQ-001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Eliminar_CuandoElServicioFalla_MuestraErrorYNoPropagaLaExcepcion()
    {
        var vm = CrearViewModel(out var equipoService, out _, out var dialogService);
        vm.EquipoSeleccionado = CrearEquipo("EQ-001");
        dialogService.Setup(d => d.ShowConfirm(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        equipoService.Setup(s => s.EliminarAsync("EQ-001", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.EquipoEnPrestamoActivo, "No se puede eliminar: préstamo activo."));

        var excepcion = await Record.ExceptionAsync(() => vm.EliminarCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        dialogService.Verify(d => d.ShowError(It.Is<string>(m => m.Contains("préstamo activo")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ImportarExcel_SinArchivoElegido_NoLlamaAlServicio()
    {
        var vm = CrearViewModel(out var equipoService, out _, out var dialogService);
        dialogService.Setup(d => d.ShowOpenFileDialog(It.IsAny<string>(), It.IsAny<string>())).Returns((string?)null);

        await vm.ImportarExcelCommand.ExecuteAsync(null);

        equipoService.Verify(s => s.ImportarDesdeExcelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportarExcel_CopiaElArchivoGeneradoAlDestinoElegido()
    {
        var vm = CrearViewModel(out _, out var reporteService, out var dialogService);

        var origen = Path.GetTempFileName();
        await File.WriteAllTextAsync(origen, "contenido-de-prueba");
        var destino = Path.Combine(Path.GetTempPath(), $"destino_{Guid.NewGuid():N}.xlsx");

        try
        {
            dialogService.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(destino);
            reporteService.Setup(r => r.ExportarInventarioExcelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(origen);

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
