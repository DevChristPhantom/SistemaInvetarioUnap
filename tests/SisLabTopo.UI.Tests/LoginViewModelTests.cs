using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Services;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.Shell;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="LoginViewModel"/> sin renderizar UI real: <see cref="IAuthService"/>
/// y <see cref="INavigationService"/> se simulan con Moq, tal como espejan las pruebas
/// de <c>AuthServiceTests</c> el comportamiento de bloqueo, pero desde el punto de vista
/// del ViewModel (mensaje mostrado, si se navega o no, si el comando queda deshabilitado).
/// </summary>
public class LoginViewModelTests
{
    private static char[] Pwd(string valor) => valor.ToCharArray();

    private static LoginViewModel CrearViewModel(
        Mock<IAuthService> authService,
        Mock<INavigationService> navigationService)
    {
        return new LoginViewModel(authService.Object, navigationService.Object, NullLogger<LoginViewModel>.Instance);
    }

    [Fact]
    public async Task IniciarSesion_ConContrasenaCorrecta_NavegaAlShellYLimpiaError()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.VerificarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var navigationService = new Mock<INavigationService>();
        var vm = CrearViewModel(authService, navigationService);

        await vm.IniciarSesionCommand.ExecuteAsync(Pwd("Cl4ve-Correcta!"));

        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        navigationService.Verify(n => n.NavigateTo<ShellViewModel>(), Times.Once);
    }

    [Fact]
    public async Task IniciarSesion_ConContrasenaIncorrecta_MuestraErrorYNoNavega()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.VerificarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        authService.Setup(a => a.ObtenerSegundosBloqueoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0L);
        authService.Setup(a => a.ObtenerIntentosFallidosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var navigationService = new Mock<INavigationService>();
        var vm = CrearViewModel(authService, navigationService);

        await vm.IniciarSesionCommand.ExecuteAsync(Pwd("Cl4ve-Incorrecta"));

        Assert.Contains("incorrecta", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1", vm.ErrorMessage);
        Assert.False(vm.IsLocked);
        navigationService.Verify(n => n.NavigateTo<ShellViewModel>(), Times.Never);
        navigationService.Verify(n => n.NavigateTo(It.IsAny<Type>()), Times.Never);
    }

    [Fact]
    public async Task IniciarSesion_SinContrasena_MuestraErrorYNoLlamaAlServicio()
    {
        var authService = new Mock<IAuthService>();
        var navigationService = new Mock<INavigationService>();
        var vm = CrearViewModel(authService, navigationService);

        await vm.IniciarSesionCommand.ExecuteAsync(Array.Empty<char>());

        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        authService.Verify(a => a.VerificarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IniciarSesion_TercerFalloConsecutivo_QuedaBloqueadoYComandoDeshabilitado()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.VerificarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        authService.Setup(a => a.ObtenerSegundosBloqueoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(30L);

        var navigationService = new Mock<INavigationService>();
        var vm = CrearViewModel(authService, navigationService);

        await vm.IniciarSesionCommand.ExecuteAsync(Pwd("Cl4ve-Incorrecta"));

        Assert.True(vm.IsLocked);
        Assert.Contains("bloqueada", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30", vm.ErrorMessage);
        Assert.False(vm.IniciarSesionCommand.CanExecute(Pwd("cualquier-cosa")));
    }

    [Fact]
    public async Task BloqueoYaVigenteAlAbrirLaPantalla_DejaElComandoNoEjecutable()
    {
        // Simula reabrir la pantalla de Login mientras un bloqueo previo sigue vigente
        // (posible porque el bloqueo se persiste en base de datos, a diferencia de Java).
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ObtenerSegundosBloqueoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(15L);

        var navigationService = new Mock<INavigationService>();
        var vm = CrearViewModel(authService, navigationService);

        Assert.True(vm.IniciarSesionCommand.CanExecute(Pwd("x")));

        await vm.ActualizarEstadoBloqueoAsync();

        Assert.True(vm.IsLocked);
        Assert.Equal(15, vm.SegundosBloqueo);
        Assert.False(vm.IniciarSesionCommand.CanExecute(Pwd("x")));
        authService.Verify(a => a.VerificarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BloqueoExpira_ReactivaElComandoYLimpiaElError()
    {
        var authService = new Mock<IAuthService>();
        var segundosRestantes = 2L;
        authService.Setup(a => a.ObtenerSegundosBloqueoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => segundosRestantes);

        var navigationService = new Mock<INavigationService>();
        var vm = CrearViewModel(authService, navigationService);

        await vm.ActualizarEstadoBloqueoAsync();
        Assert.True(vm.IsLocked);

        segundosRestantes = 0;
        await vm.ActualizarEstadoBloqueoAsync();

        Assert.False(vm.IsLocked);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.True(vm.IniciarSesionCommand.CanExecute(Pwd("x")));
    }
}
