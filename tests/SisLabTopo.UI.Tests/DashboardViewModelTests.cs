using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;
using SisLabTopo.Services;
using SisLabTopo.UI.Dashboard;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Prestamos;
using SisLabTopo.UI.Theming;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="DashboardViewModel"/>: mapeo correcto de las 4 tarjetas de
/// estadística, del donut de estado de equipos y de las mini-tablas -- incluyendo
/// explícitamente el caso de 0 equipos/0 préstamos, que debe producir un estado vacío
/// (banderas <c>Hay...</c> en falso) y NUNCA una excepción -- y de las Acciones Rápidas
/// que abren diálogos vía <see cref="IDialogService"/>.
/// </summary>
public class DashboardViewModelTests
{
    /// <summary>
    /// Fake en memoria de <see cref="IDashboardHistoryStore"/>: evita que las pruebas
    /// lean/escriban el archivo real de historial en %LOCALAPPDATA% (que además haría
    /// que una ejecución de pruebas contaminara la "foto anterior" de la siguiente,
    /// perdiendo el aislamiento entre pruebas).
    /// </summary>
    private sealed class FakeDashboardHistoryStore : IDashboardHistoryStore
    {
        private readonly List<DashboardSnapshot> _historial = new();

        public DashboardSnapshot? AnteriorParaDevolver { get; set; }

        public DashboardSnapshot? ObtenerAnterior() => AnteriorParaDevolver;

        public IReadOnlyList<DashboardSnapshot> ObtenerSerie(int maxPuntos) => _historial.TakeLast(maxPuntos).ToList();

        public void Guardar(DashboardSnapshot snapshot) => _historial.Add(snapshot);
    }

    private static Equipo CrearEquipo(string codigo, EstadoEquipo estado = EstadoEquipo.Bueno, bool disponible = true) => new()
    {
        Codigo = codigo,
        Denominacion = "Estación Total",
        Estado = estado,
        Tipo = TipoEquipo.EstacionTotal,
        Disponible = disponible,
    };

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

    private static DashboardViewModel CrearViewModel(
        out Mock<IEquipoService> equipoService,
        out Mock<IPrestamoService> prestamoService,
        out Mock<IReporteService> reporteService,
        out Mock<IDialogService> dialogService)
    {
        equipoService = new Mock<IEquipoService>();
        prestamoService = new Mock<IPrestamoService>();
        reporteService = new Mock<IReporteService>();
        dialogService = new Mock<IDialogService>();
        var comprobanteGenerator = new Mock<IComprobantePrestamoGenerator>();

        // Valores neutros por defecto para todo lo que CargarAsync consulta siempre --
        // cada prueba sobrescribe únicamente lo que le interesa verificar.
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo>());
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo>());
        equipoService.Setup(s => s.ContarPorEstadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<EstadoEquipo, int>());
        prestamoService.Setup(s => s.ContarActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        prestamoService.Setup(s => s.ContarDevueltosHoyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        prestamoService.Setup(s => s.PrestamosPorMesAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PrestamosPorMesItem>());
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo>());
        prestamoService.Setup(s => s.EquiposMasPrestadosAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new List<EquipoConteo>());
        // ConstruirTimelineItemAsync (fila 3 del Dashboard, Fase B) llama a
        // ObtenerDetalleAsync por cada préstamo activo reciente -- sin este default, Moq
        // (loose mock) devuelve una Task<List<DetallePrestamo>> con resultado null para
        // cualquier invocación no configurada explícitamente, y el ViewModel revienta con
        // NullReferenceException al hacer detalles.Count.
        prestamoService.Setup(s => s.ObtenerDetalleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo>());

        return new DashboardViewModel(
            equipoService.Object, prestamoService.Object, reporteService.Object, comprobanteGenerator.Object,
            dialogService.Object, NullLogger<DashboardViewModel>.Instance, new Mock<IThemeService>().Object,
            new FakeDashboardHistoryStore());
    }

    [Fact]
    public async Task Cargar_SinDatos_MuestraEstadoVacioSinLanzarExcepcion()
    {
        var vm = CrearViewModel(out _, out _, out _, out _);

        var excepcion = await Record.ExceptionAsync(() => vm.CargarCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.Equal("0", vm.TotalEquiposTexto);
        Assert.Equal("0", vm.EquiposDisponiblesTexto);
        Assert.Equal("0", vm.PrestamosActivosTexto);
        Assert.Equal("0", vm.DevolucionesHoyTexto);
        Assert.False(vm.HayDatosGraficoEstado);
        Assert.True(vm.SinDatosGraficoEstado);
        Assert.Empty(vm.SeriesEstadoEquipos);
        Assert.False(vm.HayPrestamosActivosRecientes);
        Assert.True(vm.SinPrestamosActivosRecientes);
        Assert.False(vm.HayEquiposMasPrestados);
        Assert.True(vm.SinEquiposMasPrestados);
        Assert.Empty(vm.EquiposDisponiblesRapida);
    }

    [Fact]
    public async Task Cargar_ConDatos_CalculaLasCuatroTarjetasDeEstadistica()
    {
        var vm = CrearViewModel(out var equipoService, out var prestamoService, out _, out _);
        var disponible1 = CrearEquipo("EQ-001", disponible: true);
        var disponible2 = CrearEquipo("EQ-002", disponible: true);
        var prestado = CrearEquipo("EQ-003", disponible: false);

        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { disponible1, disponible2, prestado });
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { disponible1, disponible2 });
        prestamoService.Setup(s => s.ContarActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(4);
        prestamoService.Setup(s => s.ContarDevueltosHoyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Equal("3", vm.TotalEquiposTexto);
        Assert.Equal("2", vm.EquiposDisponiblesTexto);
        Assert.Equal("4", vm.PrestamosActivosTexto);
        Assert.Equal("2", vm.DevolucionesHoyTexto);
        Assert.Equal(2, vm.EquiposDisponiblesRapida.Count);
    }

    [Fact]
    public async Task Cargar_ConEquipos_ConstruyeUnaPorcionPorEstadoPresente()
    {
        var vm = CrearViewModel(out var equipoService, out _, out _, out _);
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { CrearEquipo("EQ-001"), CrearEquipo("EQ-002", EstadoEquipo.Malo) });
        equipoService.Setup(s => s.ContarPorEstadoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<EstadoEquipo, int> { [EstadoEquipo.Bueno] = 1, [EstadoEquipo.Malo] = 1 });

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.True(vm.HayDatosGraficoEstado);
        Assert.False(vm.SinDatosGraficoEstado);
        Assert.Equal(2, vm.SeriesEstadoEquipos.Length);
    }

    [Fact]
    public async Task Cargar_ConPrestamosYEquiposMasPrestados_LlenaLasMiniTablas()
    {
        var vm = CrearViewModel(out var equipoService, out var prestamoService, out _, out _);
        var p1 = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now.AddMinutes(-5));
        var p2 = CrearPrestamo("22222222-aaaa-bbbb-cccc-000000000002", DateTime.Now);
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo> { p1, p2 });
        prestamoService.Setup(s => s.EquiposMasPrestadosAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EquipoConteo> { new("EQ-001", 3) });
        equipoService.Setup(s => s.BuscarPorCodigoAsync("EQ-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CrearEquipo("EQ-001"));

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.True(vm.HayPrestamosActivosRecientes);
        Assert.Equal(2, vm.UltimosPrestamosActivos.Count);
        // El más reciente (p2, sin desplazamiento de minutos) debe aparecer primero.
        Assert.Equal(p2.Id, vm.UltimosPrestamosActivos[0].Id);

        Assert.True(vm.HayEquiposMasPrestados);
        var fila = vm.EquiposMasPrestados.Single();
        Assert.Equal("EQ-001", fila.Codigo);
        Assert.Equal("Estación Total", fila.Denominacion);
        Assert.Equal(3, fila.Cantidad);
    }

    [Fact]
    public async Task Cargar_ConPrestamosActivos_ConstruyeElTimelineConResumenDeEquipos()
    {
        var vm = CrearViewModel(out var equipoService, out var prestamoService, out _, out _);
        var conVarios = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now.AddMinutes(-5));
        var conUno = CrearPrestamo("22222222-aaaa-bbbb-cccc-000000000002", DateTime.Now);
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prestamo> { conVarios, conUno });

        prestamoService.Setup(s => s.ObtenerDetalleAsync(conVarios.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo>
            {
                new() { EquipoCodigo = "EQ-001" },
                new() { EquipoCodigo = "EQ-002" },
            });
        prestamoService.Setup(s => s.ObtenerDetalleAsync(conUno.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo> { new() { EquipoCodigo = "EQ-003" } });

        var equipo1 = CrearEquipo("EQ-001");
        equipo1.Denominacion = "Estación Total Leica";
        var equipo3 = CrearEquipo("EQ-003");
        equipo3.Denominacion = "Nivel Láser";
        equipoService.Setup(s => s.BuscarPorCodigoAsync("EQ-001", It.IsAny<CancellationToken>())).ReturnsAsync(equipo1);
        equipoService.Setup(s => s.BuscarPorCodigoAsync("EQ-003", It.IsAny<CancellationToken>())).ReturnsAsync(equipo3);

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.TimelinePrestamosActivos.Count);
        // El más reciente (conUno) primero, igual que UltimosPrestamosActivos.
        var itemMasReciente = vm.TimelinePrestamosActivos[0];
        Assert.Equal(conUno.Id, itemMasReciente.Prestamo.Id);
        Assert.Equal("Nivel Láser", itemMasReciente.EquipoResumen);

        var itemConVarios = vm.TimelinePrestamosActivos[1];
        Assert.Equal(conVarios.Id, itemConVarios.Prestamo.Id);
        Assert.Equal("Estación Total Leica (+1 más)", itemConVarios.EquipoResumen);
    }

    [Fact]
    public async Task Cargar_ConPrestamoSinDetalles_MuestraResumenDeTimelineExplicito()
    {
        var vm = CrearViewModel(out _, out var prestamoService, out _, out _);
        var prestamo = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now);
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prestamo> { prestamo });
        prestamoService.Setup(s => s.ObtenerDetalleAsync(prestamo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DetallePrestamo>());

        await vm.CargarCommand.ExecuteAsync(null);

        var item = Assert.Single(vm.TimelinePrestamosActivos);
        Assert.Equal("Sin equipos registrados", item.EquipoResumen);
    }

    [Fact]
    public async Task Cargar_ConFotoAnteriorEnHistorial_CalculaTendenciaYSparklineDeLasTarjetas()
    {
        var equipoService = new Mock<IEquipoService>();
        var prestamoService = new Mock<IPrestamoService>();
        var reporteService = new Mock<IReporteService>();
        var dialogService = new Mock<IDialogService>();
        var comprobanteGenerator = new Mock<IComprobantePrestamoGenerator>();

        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { CrearEquipo("EQ-001"), CrearEquipo("EQ-002"), CrearEquipo("EQ-003") });
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { CrearEquipo("EQ-001") });
        equipoService.Setup(s => s.ContarPorEstadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<EstadoEquipo, int>());
        prestamoService.Setup(s => s.ContarActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);
        prestamoService.Setup(s => s.ContarDevueltosHoyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        prestamoService.Setup(s => s.PrestamosPorMesAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PrestamosPorMesItem>
            {
                new(2026, 6, 4),
                new(2026, 7, 6),
            });
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo>());
        prestamoService.Setup(s => s.EquiposMasPrestadosAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new List<EquipoConteo>());

        var historyStore = new FakeDashboardHistoryStore
        {
            AnteriorParaDevolver = new DashboardSnapshot(DateTime.Now.AddDays(-1), TotalEquipos: 2, EquiposDisponibles: 2, PrestamosActivos: 3, DevolucionesHoy: 0),
        };

        var vm = new DashboardViewModel(
            equipoService.Object, prestamoService.Object, reporteService.Object, comprobanteGenerator.Object,
            dialogService.Object, NullLogger<DashboardViewModel>.Instance, new Mock<IThemeService>().Object, historyStore);

        await vm.CargarCommand.ExecuteAsync(null);

        // Total Equipos: 3 ahora vs. 2 antes -> +1.
        Assert.Equal("+1 vs. última carga", vm.TotalEquiposTrendTexto);
        Assert.Equal("Up", vm.TotalEquiposTrendDireccion);

        // Equipos Disponibles: 1 ahora vs. 2 antes -> -1.
        Assert.Equal("-1 vs. última carga", vm.EquiposDisponiblesTrendTexto);
        Assert.Equal("Down", vm.EquiposDisponiblesTrendDireccion);

        // Devoluciones Hoy: 1 ahora vs. 0 antes -> +1.
        Assert.Equal("+1 vs. última carga", vm.DevolucionesHoyTrendTexto);
        Assert.Equal("Up", vm.DevolucionesHoyTrendDireccion);

        // Préstamos Activos: comparación mensual real (6 -> 4 = +50%), no contra el historial.
        Assert.Equal("+50% vs. mes anterior", vm.PrestamosActivosTrendTexto);
        Assert.Equal("Up", vm.PrestamosActivosTrendDireccion);
        Assert.Equal(new[] { 4d, 6d }, vm.PrestamosActivosSparkline);

        // El sparkline de las 3 tarjetas restantes viene de ObtenerSerie(8) sobre el
        // FakeDashboardHistoryStore: tras el Guardar() de esta misma carga, contiene
        // exactamente 1 punto (la foto recién guardada), porque el fake arranca vacío.
        Assert.Equal(new[] { 3d }, vm.TotalEquiposSparkline);
        Assert.Equal(new[] { 1d }, vm.EquiposDisponiblesSparkline);
        Assert.Equal(new[] { 1d }, vm.DevolucionesHoySparkline);
    }

    [Fact]
    public async Task Cargar_SinFotoAnteriorEnHistorial_NoMuestraTendencia()
    {
        var vm = CrearViewModel(out _, out _, out _, out _);

        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.TotalEquiposTrendTexto);
        Assert.Equal("Flat", vm.TotalEquiposTrendDireccion);
        Assert.Equal(string.Empty, vm.EquiposDisponiblesTrendTexto);
        Assert.Equal("Flat", vm.EquiposDisponiblesTrendDireccion);
        Assert.Equal(string.Empty, vm.DevolucionesHoyTrendTexto);
        Assert.Equal("Flat", vm.DevolucionesHoyTrendDireccion);
        // Sin datos mensuales (< 2 meses), tampoco hay tendencia de Préstamos Activos.
        Assert.Equal(string.Empty, vm.PrestamosActivosTrendTexto);
        Assert.Equal("Flat", vm.PrestamosActivosTrendDireccion);
    }

    [Fact]
    public async Task RegistrarDevolucionRapida_SinPrestamosActivos_MuestraInfoYNoAbreDialogo()
    {
        var vm = CrearViewModel(out _, out var prestamoService, out _, out var dialogService);
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo>());

        await vm.RegistrarDevolucionRapidaCommand.ExecuteAsync(null);

        dialogService.Verify(d => d.ShowInfo(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        dialogService.Verify(d => d.ShowDialog(It.IsAny<SeleccionarPrestamoActivoViewModel>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarDevolucionRapida_ConPrestamosActivos_AbreSelectorYLuegoDevolucionYRecarga()
    {
        var vm = CrearViewModel(out _, out var prestamoService, out _, out var dialogService);
        var prestamo = CrearPrestamo("11111111-aaaa-bbbb-cccc-000000000001", DateTime.Now);
        prestamoService.Setup(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Prestamo> { prestamo });
        prestamoService.Setup(s => s.RegistrarDevolucionAsync(prestamo.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        dialogService.Setup(d => d.ShowDialog(It.IsAny<SeleccionarPrestamoActivoViewModel>()))
            .Returns((SeleccionarPrestamoActivoViewModel selectorVm) =>
            {
                selectorVm.ConfirmarCommand.Execute(null);
                return true;
            });
        dialogService.Setup(d => d.ShowDialog(It.IsAny<DevolucionViewModel>()))
            .Returns((DevolucionViewModel devolucionVm) =>
            {
                devolucionVm.RegistrarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                return true;
            });

        await vm.RegistrarDevolucionRapidaCommand.ExecuteAsync(null);

        prestamoService.Verify(s => s.RegistrarDevolucionAsync(prestamo.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Se recarga el dashboard tras la devolución exitosa (segunda llamada a ObtenerActivosAsync: la primera para poblar el selector).
        prestamoService.Verify(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NuevoPrestamo_SinGuardarNada_NoRecarga()
    {
        var vm = CrearViewModel(out _, out var prestamoService, out _, out var dialogService);

        // El usuario abre el diálogo y cancela (el mock nunca ejecuta GuardarCommand,
        // así que NuevoPrestamoViewModel.GuardadoExitoso queda en su valor por defecto: false).
        dialogService.Setup(d => d.ShowDialog(It.IsAny<NuevoPrestamoViewModel>())).Returns(false);

        await vm.NuevoPrestamoCommand.ExecuteAsync(null);

        prestamoService.Verify(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NuevoPrestamo_GuardadoExitoso_Recarga()
    {
        var vm = CrearViewModel(out var equipoService, out var prestamoService, out _, out var dialogService);
        prestamoService.Setup(s => s.RegistrarPrestamoAsync(It.IsAny<Prestamo>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("11111111-aaaa-bbbb-cccc-000000000001");

        // Simula que, dentro del diálogo modal (bloquea hasta cerrarse), el usuario
        // completó el formulario y confirmó "Guardar" con éxito antes de que
        // ShowDialog "regrese" -- mismo patrón que PrestamosViewModelTests para
        // DevolucionViewModel.
        dialogService.Setup(d => d.ShowDialog(It.IsAny<NuevoPrestamoViewModel>()))
            .Returns((NuevoPrestamoViewModel nuevoVm) =>
            {
                nuevoVm.Estudiante = "Juan Pérez";
                nuevoVm.CodigoEstudiante = "2020-123";
                nuevoVm.Filas[0].Asignar(CrearEquipo("EQ-001"));
                nuevoVm.GuardarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                return true;
            });

        await vm.NuevoPrestamoCommand.ExecuteAsync(null);

        prestamoService.Verify(
            s => s.RegistrarPrestamoAsync(It.IsAny<Prestamo>(), It.Is<IReadOnlyList<string>>(c => c.Contains("EQ-001")), It.IsAny<CancellationToken>()),
            Times.Once);
        // El Dashboard recarga sus datos tras el guardado exitoso.
        prestamoService.Verify(s => s.ObtenerActivosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
