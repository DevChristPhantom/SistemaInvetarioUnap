using Moq;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;
using SisLabTopo.UI.Prestamos;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="EquipoSearchViewModel"/>: puerto funcional de
/// <c>NuevoPrestamoDialog.EquipoSearchDialog</c> (Java) -- solo equipos disponibles,
/// excluye <see cref="EstadoEquipo.Chatarra"/> y los códigos ya elegidos en otras filas.
/// </summary>
public class EquipoSearchViewModelTests
{
    private static Equipo Crear(string codigo, EstadoEquipo estado = EstadoEquipo.Bueno) => new()
    {
        Codigo = codigo,
        Denominacion = $"Equipo {codigo}",
        Estado = estado,
        Disponible = true,
    };

    [Fact]
    public async Task Cargar_ExcluyeChatarraYCodigosYaSeleccionados()
    {
        var equipoService = new Mock<IEquipoService>();
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo>
        {
            Crear("EQ-001"),
            Crear("EQ-002", EstadoEquipo.Chatarra),
            Crear("EQ-003"),
        });

        var vm = new EquipoSearchViewModel(equipoService.Object, new[] { "EQ-003" });
        await vm.CargarCommand.ExecuteAsync(null);

        Assert.Single(vm.EquiposDisponibles);
        Assert.Equal("EQ-001", vm.EquiposDisponibles[0].Codigo);
    }

    [Fact]
    public void Seleccionar_SinResaltarNinguno_QuedaDeshabilitado()
    {
        var equipoService = new Mock<IEquipoService>();
        var vm = new EquipoSearchViewModel(equipoService.Object, Array.Empty<string>());

        Assert.False(vm.SeleccionarCommand.CanExecute(null));
    }

    [Fact]
    public void Seleccionar_ConResaltado_AsignaEquipoSeleccionadoYDisparaCierre()
    {
        var equipoService = new Mock<IEquipoService>();
        var vm = new EquipoSearchViewModel(equipoService.Object, Array.Empty<string>());
        var equipo = Crear("EQ-005");
        vm.EquipoResaltado = equipo;

        var seCerro = false;
        vm.SolicitarCierre += (_, _) => seCerro = true;

        Assert.True(vm.SeleccionarCommand.CanExecute(null));
        vm.SeleccionarCommand.Execute(null);

        Assert.Same(equipo, vm.EquipoSeleccionado);
        Assert.True(seCerro);
    }

    [Fact]
    public void Cancelar_NoAsignaEquipoSeleccionadoPeroDisparaCierre()
    {
        var equipoService = new Mock<IEquipoService>();
        var vm = new EquipoSearchViewModel(equipoService.Object, Array.Empty<string>());

        var seCerro = false;
        vm.SolicitarCierre += (_, _) => seCerro = true;

        vm.CancelarCommand.Execute(null);

        Assert.Null(vm.EquipoSeleccionado);
        Assert.True(seCerro);
    }
}
