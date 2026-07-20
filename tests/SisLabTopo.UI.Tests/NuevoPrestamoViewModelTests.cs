using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Prestamos;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="NuevoPrestamoViewModel"/>: el límite fijo de 6 ítems (no
/// dinámico, por decisión explícita del plan de migración), validación por campo de los
/// datos del solicitante, exigencia de al menos un equipo, y que
/// <see cref="IPrestamoService.RegistrarPrestamoAsync"/> reciba exactamente los códigos
/// elegidos.
/// </summary>
public class NuevoPrestamoViewModelTests
{
    private static NuevoPrestamoViewModel CrearViewModel(
        out Mock<IPrestamoService> prestamoService,
        out Mock<IEquipoService> equipoService,
        out Mock<IComprobantePrestamoGenerator> comprobanteGenerator,
        out Mock<IDialogService> dialogService)
    {
        prestamoService = new Mock<IPrestamoService>();
        equipoService = new Mock<IEquipoService>();
        comprobanteGenerator = new Mock<IComprobantePrestamoGenerator>();
        dialogService = new Mock<IDialogService>();
        return new NuevoPrestamoViewModel(prestamoService.Object, equipoService.Object, comprobanteGenerator.Object, dialogService.Object);
    }

    [Fact]
    public void Filas_SiempreTieneExactamenteSeis()
    {
        var vm = CrearViewModel(out _, out _, out _, out _);

        Assert.Equal(6, vm.Filas.Count);
    }

    [Fact]
    public async Task Guardar_SinEstudianteNiCodigoEstudiante_NoLlamaAlServicioYQuedaEnError()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _, out _);
        vm.Estudiante = string.Empty;
        vm.CodigoEstudiante = string.Empty;
        vm.Filas[0].Asignar(new Equipo { Codigo = "EQ-001", Denominacion = "Estación Total", Estado = EstadoEquipo.Bueno });

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.True(vm.HasErrors);
        prestamoService.Verify(
            s => s.RegistrarPrestamoAsync(It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Guardar_SinNingunEquipoSeleccionado_MuestraMensajeYNoLlamaAlServicio()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _, out _);
        vm.Estudiante = "Juan Pérez";
        vm.CodigoEstudiante = "2020-123";

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.Contains("al menos un equipo", vm.MensajeError, StringComparison.OrdinalIgnoreCase);
        prestamoService.Verify(
            s => s.RegistrarPrestamoAsync(It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Guardar_ConDatosValidos_RegistraElPrestamoConLosCodigosElegidos()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _, out _);
        vm.Estudiante = "Juan Pérez";
        vm.CodigoEstudiante = "2020-123";
        vm.Filas[0].Asignar(new Equipo { Codigo = "EQ-001", Denominacion = "Estación Total", Estado = EstadoEquipo.Bueno });
        vm.Filas[2].Asignar(new Equipo { Codigo = "EQ-003", Denominacion = "Trípode", Estado = EstadoEquipo.Bueno });

        IReadOnlyList<string>? codigosRecibidos = null;
        prestamoService.Setup(s => s.RegistrarPrestamoAsync(It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<Prestamo, IReadOnlyList<string>, CancellationToken>((_, codigos, _) => codigosRecibidos = codigos)
            .ReturnsAsync("prestamo-generado-id");

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.True(vm.GuardadoExitoso);
        Assert.NotNull(codigosRecibidos);
        Assert.Equal(new[] { "EQ-001", "EQ-003" }, codigosRecibidos);
    }

    [Fact]
    public async Task Guardar_CuandoElServicioFalla_MuestraMensajeDeErrorYNoPropagaLaExcepcion()
    {
        var vm = CrearViewModel(out var prestamoService, out _, out _, out _);
        vm.Estudiante = "Juan Pérez";
        vm.CodigoEstudiante = "2020-123";
        vm.Filas[0].Asignar(new Equipo { Codigo = "EQ-001", Denominacion = "Estación Total", Estado = EstadoEquipo.Bueno });

        prestamoService.Setup(s => s.RegistrarPrestamoAsync(It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.EquipoNoDisponible, "El equipo con código EQ-001 no está disponible."));

        var excepcion = await Record.ExceptionAsync(() => vm.GuardarCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.False(vm.GuardadoExitoso);
        Assert.Contains("no está disponible", vm.MensajeError);
    }

    [Fact]
    public void Buscar_ExcluyeCodigosYaElegidosEnOtrasFilas()
    {
        var vm = CrearViewModel(out _, out var equipoService, out _, out var dialogService);
        vm.Filas[0].Asignar(new Equipo { Codigo = "EQ-001", Denominacion = "Estación Total", Estado = EstadoEquipo.Bueno });

        var disponibles = new List<Equipo>
        {
            new() { Codigo = "EQ-001", Denominacion = "Estación Total", Estado = EstadoEquipo.Bueno, Disponible = true },
            new() { Codigo = "EQ-002", Denominacion = "Trípode", Estado = EstadoEquipo.Bueno, Disponible = true },
        };
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(disponibles);

        // Simula que, dentro del diálogo modal (bloqueante), su carga inicial corrió
        // antes de que ShowDialog "regrese", para poder inspeccionar el resultado
        // filtrado sin necesitar una ventana WPF real.
        EquipoSearchViewModel? busquedaAbierta = null;
        dialogService.Setup(d => d.ShowDialog(It.IsAny<EquipoSearchViewModel>()))
            .Returns((EquipoSearchViewModel vmBusqueda) =>
            {
                busquedaAbierta = vmBusqueda;
                vmBusqueda.CargarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                return true;
            });

        vm.BuscarCommand.Execute(vm.Filas[1]);

        Assert.NotNull(busquedaAbierta);
        Assert.DoesNotContain(busquedaAbierta!.EquiposDisponibles, e => e.Codigo == "EQ-001");
        Assert.Contains(busquedaAbierta.EquiposDisponibles, e => e.Codigo == "EQ-002");
    }
}
