using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.Login;
using SisLabTopo.UI.Navigation;
using SisLabTopo.UI.Startup;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="FirstRunSetupViewModel"/> (Fase 6): validación inline de la
/// contraseña inicial (longitud mínima, coincidencia de confirmación), el flujo de
/// creación de cuenta que expone el código de recuperación una única vez, y que
/// "Continuar" exige haber confirmado el guardado del código antes de navegar al Login.
/// </summary>
public class FirstRunSetupViewModelTests
{
    private static FirstRunSetupViewModel CrearViewModel(
        out Mock<IAuthService> authService,
        out Mock<INavigationService> navigationService,
        out Mock<IDialogService> dialogService)
    {
        authService = new Mock<IAuthService>();
        navigationService = new Mock<INavigationService>();
        dialogService = new Mock<IDialogService>();
        return new FirstRunSetupViewModel(authService.Object, navigationService.Object, dialogService.Object, NullLogger<FirstRunSetupViewModel>.Instance);
    }

    [Fact]
    public void SinNadaEscrito_NoMuestraMensajesDeErrorYComandoDeshabilitado()
    {
        var vm = CrearViewModel(out _, out _, out _);

        Assert.Equal(string.Empty, vm.MensajeErrorNueva);
        Assert.Equal(string.Empty, vm.MensajeErrorConfirmar);
        Assert.False(vm.CrearCuentaCommand.CanExecute(null));
    }

    [Fact]
    public void ContrasenaMuyCorta_MuestraMensajeInlineYDeshabilitaCrearCuenta()
    {
        var vm = CrearViewModel(out _, out _, out _);
        vm.NuevaContrasena = "123";
        vm.ConfirmarContrasena = "123";

        Assert.False(string.IsNullOrEmpty(vm.MensajeErrorNueva));
        Assert.False(vm.CrearCuentaCommand.CanExecute(null));
    }

    [Fact]
    public void ContrasenasNoCoinciden_MuestraMensajeInlineYDeshabilitaCrearCuenta()
    {
        var vm = CrearViewModel(out _, out _, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "otraClave456";

        Assert.False(string.IsNullOrEmpty(vm.MensajeErrorConfirmar));
        Assert.False(vm.CrearCuentaCommand.CanExecute(null));
    }

    [Fact]
    public void DatosValidos_HabilitaCrearCuentaYSinMensajesDeError()
    {
        var vm = CrearViewModel(out _, out _, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";

        Assert.True(string.IsNullOrEmpty(vm.MensajeErrorNueva));
        Assert.True(string.IsNullOrEmpty(vm.MensajeErrorConfirmar));
        Assert.True(vm.CrearCuentaCommand.CanExecute(null));
    }

    [Fact]
    public async Task CrearCuenta_Exitoso_MuestraElCodigoDeRecuperacionYLimpiaLosCampos()
    {
        var vm = CrearViewModel(out var authService, out _, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.ConfigurarContrasenaInicialAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ABCD-EFGH-JKMN-PQRS");

        await vm.CrearCuentaCommand.ExecuteAsync(null);

        Assert.True(vm.MostrandoCodigo);
        Assert.Equal("ABCD-EFGH-JKMN-PQRS", vm.CodigoRecuperacion);
        Assert.Empty(vm.NuevaContrasena);
        Assert.Empty(vm.ConfirmarContrasena);
        Assert.Empty(vm.MensajeError);
    }

    [Fact]
    public async Task CrearCuenta_ErrorDelServicio_MuestraMensajeInlineYNoAvanzaDePaso()
    {
        var vm = CrearViewModel(out var authService, out _, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.ConfigurarContrasenaInicialAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.ContrasenaYaConfigurada, "Ya existe una contraseña de administrador configurada."));

        var excepcion = await Record.ExceptionAsync(() => vm.CrearCuentaCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.False(vm.MostrandoCodigo);
        Assert.Contains("configurada", vm.MensajeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopiarCodigo_CopiaElCodigoVigenteAlPortapapeles()
    {
        var vm = CrearViewModel(out var authService, out _, out var dialogService);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.ConfigurarContrasenaInicialAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("WXYZ-2345-6789-ABCD");
        await vm.CrearCuentaCommand.ExecuteAsync(null);

        vm.CopiarCodigoCommand.Execute(null);

        dialogService.Verify(d => d.CopyToClipboard("WXYZ-2345-6789-ABCD"), Times.Once);
    }

    [Fact]
    public async Task Continuar_SinConfirmarQueGuardoElCodigo_ComandoDeshabilitado()
    {
        var vm = CrearViewModel(out var authService, out _, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.ConfigurarContrasenaInicialAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("CODE-0000-1111-2222");
        await vm.CrearCuentaCommand.ExecuteAsync(null);

        Assert.False(vm.ContinuarCommand.CanExecute(null));
    }

    [Fact]
    public async Task Continuar_ConCheckboxConfirmado_NavegaALoginYVaciaElCodigoDeLaMemoria()
    {
        var vm = CrearViewModel(out var authService, out var navigationService, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.ConfigurarContrasenaInicialAsync(It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("CODE-0000-1111-2222");
        await vm.CrearCuentaCommand.ExecuteAsync(null);

        vm.ConfirmoQueGuardeElCodigo = true;
        Assert.True(vm.ContinuarCommand.CanExecute(null));

        vm.ContinuarCommand.Execute(null);

        navigationService.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
        Assert.Empty(vm.CodigoRecuperacion);
    }
}
