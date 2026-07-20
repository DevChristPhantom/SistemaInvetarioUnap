using Moq;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Dialogs;
using SisLabTopo.UI.PasswordRecovery;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="PasswordRecoveryViewModel"/> (Fase 6): validación inline
/// (contraseña nueva muy corta, confirmación que no coincide, código vacío), el flujo
/// exitoso que muestra el nuevo código de recuperación una sola vez, el manejo de un
/// código incorrecto (error inline, sin cerrar el diálogo), y que "Cerrar" en el paso del
/// código nuevo exige haber marcado la casilla de confirmación.
/// </summary>
public class PasswordRecoveryViewModelTests
{
    private static PasswordRecoveryViewModel CrearViewModel(
        out Mock<IAuthService> authService,
        out Mock<IDialogService> dialogService)
    {
        authService = new Mock<IAuthService>();
        dialogService = new Mock<IDialogService>();
        return new PasswordRecoveryViewModel(authService.Object, dialogService.Object);
    }

    [Fact]
    public void SinNadaEscrito_ComandoRestablecerDeshabilitado()
    {
        var vm = CrearViewModel(out _, out _);

        Assert.False(vm.RestablecerCommand.CanExecute(null));
    }

    [Fact]
    public void NuevaContrasenaMuyCorta_MuestraMensajeInlineYDeshabilitaRestablecer()
    {
        var vm = CrearViewModel(out _, out _);
        vm.CodigoRecuperacion = "ABCD-EFGH-JKMN-PQRS";
        vm.NuevaContrasena = "123";
        vm.ConfirmarContrasena = "123";

        Assert.False(string.IsNullOrEmpty(vm.MensajeErrorNueva));
        Assert.False(vm.RestablecerCommand.CanExecute(null));
    }

    [Fact]
    public void ContrasenasNoCoinciden_MuestraMensajeInlineYDeshabilitaRestablecer()
    {
        var vm = CrearViewModel(out _, out _);
        vm.CodigoRecuperacion = "ABCD-EFGH-JKMN-PQRS";
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "otraClave456";

        Assert.False(string.IsNullOrEmpty(vm.MensajeErrorConfirmar));
        Assert.False(vm.RestablecerCommand.CanExecute(null));
    }

    [Fact]
    public void SinCodigoIngresado_DeshabilitaRestablecer_AunConContrasenasValidas()
    {
        var vm = CrearViewModel(out _, out _);
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";

        Assert.False(vm.RestablecerCommand.CanExecute(null));
    }

    [Fact]
    public void DatosValidos_HabilitaRestablecer()
    {
        var vm = CrearViewModel(out _, out _);
        vm.CodigoRecuperacion = "ABCD-EFGH-JKMN-PQRS";
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";

        Assert.True(vm.RestablecerCommand.CanExecute(null));
    }

    [Fact]
    public async Task Restablecer_CodigoCorrecto_MuestraNuevoCodigoYMarcaExitoso()
    {
        var vm = CrearViewModel(out var authService, out _);
        vm.CodigoRecuperacion = "ABCD-EFGH-JKMN-PQRS";
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.RestablecerContrasenaConCodigoAsync(It.IsAny<char[]>(), It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NEW1-2345-6789-ABCD");

        await vm.RestablecerCommand.ExecuteAsync(null);

        Assert.True(vm.RestablecidoExitoso);
        Assert.True(vm.MostrandoCodigoNuevo);
        Assert.Equal("NEW1-2345-6789-ABCD", vm.CodigoNuevo);
        Assert.Empty(vm.MensajeError);
        Assert.Empty(vm.CodigoRecuperacion);
        Assert.Empty(vm.NuevaContrasena);
        Assert.Empty(vm.ConfirmarContrasena);
    }

    [Fact]
    public async Task Restablecer_CodigoIncorrecto_MuestraErrorInlineYNoAvanzaDePaso()
    {
        var vm = CrearViewModel(out var authService, out _);
        vm.CodigoRecuperacion = "CODIGO-INVALIDO";
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.RestablecerContrasenaConCodigoAsync(It.IsAny<char[]>(), It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.CodigoRecuperacionInvalido, "El código de recuperación ingresado no es válido."));

        var excepcion = await Record.ExceptionAsync(() => vm.RestablecerCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.False(vm.RestablecidoExitoso);
        Assert.False(vm.MostrandoCodigoNuevo);
        Assert.Contains("válido", vm.MensajeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CopiarCodigo_CopiaElCodigoNuevoAlPortapapeles()
    {
        var vm = CrearViewModel(out _, out var dialogService);
        vm.CodigoNuevo = "NEW1-2345-6789-ABCD";

        vm.CopiarCodigoCommand.Execute(null);

        dialogService.Verify(d => d.CopyToClipboard("NEW1-2345-6789-ABCD"), Times.Once);
    }

    [Fact]
    public void CerrarEnPaso1_SiempreDisponible_SolicitaCierreSinExito()
    {
        var vm = CrearViewModel(out _, out _);
        var solicito = false;
        vm.SolicitarCierre += (_, _) => solicito = true;

        Assert.True(vm.CerrarCommand.CanExecute(null));
        vm.CerrarCommand.Execute(null);

        Assert.True(solicito);
        Assert.False(vm.RestablecidoExitoso);
    }

    [Fact]
    public async Task CerrarEnPaso2_SinConfirmarCheckbox_ComandoDeshabilitado()
    {
        var vm = CrearViewModel(out var authService, out _);
        vm.CodigoRecuperacion = "ABCD-EFGH-JKMN-PQRS";
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.RestablecerContrasenaConCodigoAsync(It.IsAny<char[]>(), It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NEW1-2345-6789-ABCD");
        await vm.RestablecerCommand.ExecuteAsync(null);

        Assert.False(vm.CerrarCommand.CanExecute(null));
    }

    [Fact]
    public async Task CerrarEnPaso2_ConCheckboxConfirmado_SolicitaCierreYVaciaElCodigoNuevo()
    {
        var vm = CrearViewModel(out var authService, out _);
        vm.CodigoRecuperacion = "ABCD-EFGH-JKMN-PQRS";
        vm.NuevaContrasena = "clave123";
        vm.ConfirmarContrasena = "clave123";
        authService.Setup(a => a.RestablecerContrasenaConCodigoAsync(It.IsAny<char[]>(), It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NEW1-2345-6789-ABCD");
        await vm.RestablecerCommand.ExecuteAsync(null);

        vm.ConfirmoQueGuardeElCodigo = true;
        Assert.True(vm.CerrarCommand.CanExecute(null));

        var solicito = false;
        vm.SolicitarCierre += (_, _) => solicito = true;
        vm.CerrarCommand.Execute(null);

        Assert.True(solicito);
        Assert.Empty(vm.CodigoNuevo);
    }
}
