using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.Shell;
using SisLabTopo.UI.Theming;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Regresión de un bug real encontrado en QA manual (recorrido con la app empaquetada
/// de verdad, no solo pruebas automatizadas): la barra de estado inferior del Shell
/// ("Equipos Disponibles: X de Y") solo se refrescaba al navegar entre pantallas
/// (<see cref="ShellViewModel.ActualizarBarraEstadoAsync"/>, llamado desde
/// <c>Navegar&lt;T&gt;</c>). Si el usuario completaba un préstamo de 6 equipos sin
/// salir de la pantalla de Préstamos, la barra seguía mostrando el conteo previo
/// hasta que navegaba a otra pantalla y volvía -- verificado manualmente: mostraba
/// "Equipos Disponibles: 6 de 6" justo después de prestar los 6, cuando el dato real
/// ya era "0 de 6". Ver <see cref="InventarioCambiadoMessage"/> para la solución
/// (WeakReferenceMessenger) y sus puntos de envío en EquiposViewModel/PrestamosViewModel.
/// </summary>
public class ShellViewModelTests
{
    [Fact]
    public async Task InventarioCambiadoMessage_RefrescaBarraDeEstadoSinNecesidadDeNavegar()
    {
        var equipoService = new Mock<IEquipoService>();
        var llamada = 0;
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                llamada++;
                // Primera carga: 0 disponibles (los 6 recién prestados). Segunda carga
                // (tras el mensaje, simulando una devolución): 1 disponible.
                return llamada == 1 ? new List<Equipo>() : new List<Equipo> { new() { Codigo = "X" } };
            });
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Equipo> { new() { Codigo = "X" } });

        var shellVm = new ShellViewModel(
            new Mock<INavigationService>().Object, equipoService.Object,
            new ThemeService(), NullLogger<ShellViewModel>.Instance);

        await shellVm.ActualizarBarraEstadoAsync();
        Assert.Equal("Equipos Disponibles: 0 de 1", shellVm.EstadoEquiposTexto);

        // Sin llamar a ningún comando de navegación: solo el mensaje, como lo enviarían
        // EquiposViewModel/PrestamosViewModel tras una mutación exitosa.
        WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
        await Task.Delay(200);

        Assert.Equal("Equipos Disponibles: 1 de 1", shellVm.EstadoEquiposTexto);

        shellVm.DetenerReloj();
    }

    [Fact]
    public void DetenerReloj_DesregistraElMensaje_NoActualizaTrasLlamarlo()
    {
        var equipoService = new Mock<IEquipoService>();
        equipoService.Setup(s => s.ObtenerDisponiblesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo>());
        equipoService.Setup(s => s.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Equipo>());

        var shellVm = new ShellViewModel(
            new Mock<INavigationService>().Object, equipoService.Object,
            new ThemeService(), NullLogger<ShellViewModel>.Instance);

        shellVm.DetenerReloj();

        // No debe lanzar (des-registrar dos veces, o enviar tras des-registrar, debe ser inofensivo).
        WeakReferenceMessenger.Default.Send(new InventarioCambiadoMessage());
        shellVm.DetenerReloj();
    }
}
