using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Services;
using SisLabTopo.UI.Configuracion;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de <see cref="ConfiguracionViewModel"/>: validación inline de cambio de
/// contraseña -- contraseñas que no coinciden, nueva contraseña muy corta, y
/// contraseña actual incorrecta (reflejada como error inline, nunca un MessageBox
/// bloqueante ni una excepción sin manejar).
/// </summary>
public class ConfiguracionViewModelTests
{
    private static ConfiguracionViewModel CrearViewModel(out Mock<IAuthService> authService)
    {
        authService = new Mock<IAuthService>();
        return new ConfiguracionViewModel(authService.Object, NullLogger<ConfiguracionViewModel>.Instance);
    }

    [Fact]
    public void NuevaContrasenaMuyCorta_MuestraMensajeInlineYDeshabilitaGuardar()
    {
        var vm = CrearViewModel(out _);
        vm.ContrasenaActual = "actual123";
        vm.NuevaContrasena = "123";
        vm.ConfirmarContrasena = "123";

        Assert.False(string.IsNullOrEmpty(vm.MensajeErrorNueva));
        Assert.False(vm.GuardarCommand.CanExecute(null));
    }

    [Fact]
    public void ContrasenasNoCoinciden_MuestraMensajeInlineYDeshabilitaGuardar()
    {
        var vm = CrearViewModel(out _);
        vm.ContrasenaActual = "actual123";
        vm.NuevaContrasena = "nueva123";
        vm.ConfirmarContrasena = "otraDistinta";

        Assert.False(string.IsNullOrEmpty(vm.MensajeErrorConfirmar));
        Assert.False(vm.GuardarCommand.CanExecute(null));
    }

    [Fact]
    public void DatosValidos_HabilitaGuardarYSinMensajesDeError()
    {
        var vm = CrearViewModel(out _);
        vm.ContrasenaActual = "actual123";
        vm.NuevaContrasena = "nueva123";
        vm.ConfirmarContrasena = "nueva123";

        Assert.True(string.IsNullOrEmpty(vm.MensajeErrorNueva));
        Assert.True(string.IsNullOrEmpty(vm.MensajeErrorConfirmar));
        Assert.True(vm.GuardarCommand.CanExecute(null));
    }

    [Fact]
    public void SinNadaEscrito_NoMuestraMensajesDeErrorTodavia()
    {
        var vm = CrearViewModel(out _);

        Assert.Equal(string.Empty, vm.MensajeErrorNueva);
        Assert.Equal(string.Empty, vm.MensajeErrorConfirmar);
        Assert.False(vm.GuardarCommand.CanExecute(null));
    }

    [Fact]
    public async Task Guardar_ContrasenaActualIncorrecta_MuestraErrorInlineYNoPropagaExcepcion()
    {
        var vm = CrearViewModel(out var authService);
        vm.ContrasenaActual = "incorrecta";
        vm.NuevaContrasena = "nueva123";
        vm.ConfirmarContrasena = "nueva123";
        authService.Setup(s => s.CambiarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceException(ErrorCode.CredencialesIncorrectas, "La contraseña actual ingresada es incorrecta."));

        var excepcion = await Record.ExceptionAsync(() => vm.GuardarCommand.ExecuteAsync(null));

        Assert.Null(excepcion);
        Assert.Contains("incorrecta", vm.MensajeError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.MensajeExito);
    }

    [Fact]
    public async Task Guardar_Exitoso_MuestraMensajeDeExitoYLimpiaLosCampos()
    {
        var vm = CrearViewModel(out var authService);
        vm.ContrasenaActual = "actual123";
        vm.NuevaContrasena = "nueva123";
        vm.ConfirmarContrasena = "nueva123";
        authService.Setup(s => s.CambiarContrasenaAsync(It.IsAny<char[]>(), It.IsAny<char[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.Equal("Contraseña actualizada con éxito.", vm.MensajeExito);
        Assert.Empty(vm.MensajeError);
        Assert.Empty(vm.ContrasenaActual);
        Assert.Empty(vm.NuevaContrasena);
        Assert.Empty(vm.ConfirmarContrasena);
    }
}
