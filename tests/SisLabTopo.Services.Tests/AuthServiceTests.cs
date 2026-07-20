using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Data;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Puerto de <c>AuthServiceTest.java</c> (3 pruebas) contra SQLite real, más una prueba
/// nueva pedida por el plan de migración: dos instancias de <see cref="AuthService"/>
/// construidas sobre la misma base de datos comparten el estado de bloqueo (verifica la
/// persistencia real en <c>AppState</c>, la mejora de seguridad de la Fase 2 sobre la
/// versión Java, donde el bloqueo vivía solo en memoria y se perdía al reiniciar).
///
/// Decisión de diseño: se usa SQLite real (no Moq) porque el propio bloqueo de 3
/// intentos es la propiedad persistida bajo prueba — mockear el repositorio de config
/// no alcanzaría para probar <c>AppState</c>, que <see cref="AuthService"/> lee/escribe
/// directamente vía <see cref="SisLabTopo.Data.SisLabTopoDbContext"/>.
///
/// Ninguna contraseña de prueba coincide con la contraseña por defecto hardcodeada que
/// tenía la versión Java ("admin123"): cada test siembra su propia contraseña de prueba
/// explícita en la base de datos vacía, y AuthService nunca asume ninguna por defecto.
/// </summary>
public class AuthServiceTests : ServicesSqliteTestBase
{
    private const string ContrasenaPrueba = "Cl4ve-Prueba-Segura!";

    // Cada CrearServicio() abre un SisLabTopoDbContext propio (para simular instancias
    // independientes de AuthService, p.ej. "reinicios" de la app). Se registran aquí
    // para desecharlos explícitamente en DisposeAsync: en Windows, un DbContext SQLite
    // sin desechar retiene un lock de archivo que ClearAllPools() por sí solo no libera,
    // y el borrado del directorio temporal de la prueba fallaría con IOException.
    private readonly List<SisLabTopoDbContext> _contexts = new();

    public override async Task DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private async Task<AuthService> CrearServicioConContrasenaSembradaAsync()
    {
        var seedContext = CreateContext();
        _contexts.Add(seedContext);
        var configRepo = CreatePrestamoRepo(seedContext);
        var hash = BCrypt.Net.BCrypt.HashPassword(ContrasenaPrueba, workFactor: 10);
        await configRepo.GuardarConfigAsync(AuthService.ClaveHashContrasena, hash);

        return CrearServicio();
    }

    private AuthService CrearServicio()
    {
        var context = CreateContext();
        _contexts.Add(context);
        var configRepo = CreatePrestamoRepo(context);
        return new AuthService(configRepo, context, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task VerificarContrasena_DeberiaRetornarTrue_CuandoEsCorrecta()
    {
        var service = await CrearServicioConContrasenaSembradaAsync();

        var resultado = await service.VerificarContrasenaAsync(ContrasenaPrueba.ToCharArray());

        Assert.True(resultado);
        Assert.Equal(0, await service.ObtenerIntentosFallidosAsync());
    }

    [Fact]
    public async Task VerificarContrasena_DeberiaRetornarFalse_CuandoEsIncorrecta()
    {
        var service = await CrearServicioConContrasenaSembradaAsync();

        var resultado = await service.VerificarContrasenaAsync("password_erroneo".ToCharArray());

        Assert.False(resultado);
        Assert.Equal(1, await service.ObtenerIntentosFallidosAsync());
    }

    [Fact]
    public async Task VerificarContrasena_DeberiaBloquear_AlTercerIntentoFallido()
    {
        var service = await CrearServicioConContrasenaSembradaAsync();

        Assert.False(await service.VerificarContrasenaAsync("erroneo".ToCharArray()));
        Assert.False(await service.VerificarContrasenaAsync("erroneo".ToCharArray()));
        Assert.False(await service.VerificarContrasenaAsync("erroneo".ToCharArray())); // 3er intento

        Assert.Equal(3, await service.ObtenerIntentosFallidosAsync());
        Assert.True(await service.ObtenerSegundosBloqueoAsync() > 0);

        // El 4to intento con la contraseña CORRECTA debe seguir fallando: el bloqueo
        // vigente rechaza incluso la contraseña correcta (invariante explícito del plan).
        Assert.False(await service.VerificarContrasenaAsync(ContrasenaPrueba.ToCharArray()));
    }

    [Fact]
    public async Task DosInstancias_SobreLaMismaBaseDeDatos_ComparteElEstadoDeBloqueo()
    {
        await using var seedContext = CreateContext();
        var configRepo = CreatePrestamoRepo(seedContext);
        var hash = BCrypt.Net.BCrypt.HashPassword(ContrasenaPrueba, workFactor: 10);
        await configRepo.GuardarConfigAsync(AuthService.ClaveHashContrasena, hash);

        var instanciaA = CrearServicio();

        // 3 fallos con la primera instancia -> bloqueo persistido en AppState.
        await instanciaA.VerificarContrasenaAsync("malo".ToCharArray());
        await instanciaA.VerificarContrasenaAsync("malo".ToCharArray());
        await instanciaA.VerificarContrasenaAsync("malo".ToCharArray());

        // Una instancia NUEVA (simula un reinicio de la aplicación) construida sobre el
        // mismo archivo SQLite debe "recordar" el bloqueo: ni siquiera la contraseña
        // correcta debe funcionar todavía.
        var instanciaB = CrearServicio();

        Assert.Equal(3, await instanciaB.ObtenerIntentosFallidosAsync());
        Assert.True(await instanciaB.ObtenerSegundosBloqueoAsync() > 0);
        Assert.False(await instanciaB.VerificarContrasenaAsync(ContrasenaPrueba.ToCharArray()));
    }

    [Fact]
    public async Task VerificarContrasena_SinHashConfigurado_RetornaFalse_SinContarComoIntentoFallido()
    {
        // Base de datos recién migrada, sin ningún hash guardado todavía (estado previo
        // al asistente de primer arranque de la Fase 6): no debe haber ninguna
        // contraseña por defecto hardcodeada que la acepte.
        var service = CrearServicio();

        var resultado = await service.VerificarContrasenaAsync("cualquiera".ToCharArray());

        Assert.False(resultado);
        Assert.Equal(0, await service.ObtenerIntentosFallidosAsync());
    }

    [Fact]
    public async Task CambiarContrasena_DeberiaFallar_CuandoNuevaContrasenaEsDebil()
    {
        var service = await CrearServicioConContrasenaSembradaAsync();

        var ex = await Assert.ThrowsAsync<SisLabTopo.Domain.Exceptions.ServiceException>(() =>
            service.CambiarContrasenaAsync(ContrasenaPrueba.ToCharArray(), "abc".ToCharArray()));

        Assert.Equal(SisLabTopo.Domain.Exceptions.ErrorCode.ContrasenaDebil, ex.Code);
    }

    [Fact]
    public async Task CambiarContrasena_DeberiaPermitirVerificarConLaNueva_DespuesDeCambiarla()
    {
        var service = await CrearServicioConContrasenaSembradaAsync();
        const string nueva = "Otra-Clave-Nueva-2";

        await service.CambiarContrasenaAsync(ContrasenaPrueba.ToCharArray(), nueva.ToCharArray());

        var otraInstancia = CrearServicio();
        Assert.True(await otraInstancia.VerificarContrasenaAsync(nueva.ToCharArray()));
    }

    // ===================== Fase 6: asistente de primer arranque =====================

    [Fact]
    public async Task ExisteContrasenaConfigurada_EsFalse_EnBaseDeDatosNueva()
    {
        var service = CrearServicio();

        Assert.False(await service.ExisteContrasenaConfiguradaAsync());
    }

    [Fact]
    public async Task ExisteContrasenaConfigurada_EsTrue_DespuesDeConfigurarContrasenaInicial()
    {
        var service = CrearServicio();

        await service.ConfigurarContrasenaInicialAsync("Primera-Clave-123".ToCharArray());

        Assert.True(await service.ExisteContrasenaConfiguradaAsync());
    }

    [Fact]
    public async Task ConfigurarContrasenaInicial_DeberiaFallar_CuandoLaContrasenaEsDebil()
    {
        var service = CrearServicio();

        var ex = await Assert.ThrowsAsync<SisLabTopo.Domain.Exceptions.ServiceException>(() =>
            service.ConfigurarContrasenaInicialAsync("abc".ToCharArray()));

        Assert.Equal(SisLabTopo.Domain.Exceptions.ErrorCode.ContrasenaDebil, ex.Code);
        Assert.False(await service.ExisteContrasenaConfiguradaAsync());
    }

    [Fact]
    public async Task ConfigurarContrasenaInicial_DeberiaFallar_CuandoYaHayUnaContrasenaConfigurada()
    {
        var service = await CrearServicioConContrasenaSembradaAsync();

        var ex = await Assert.ThrowsAsync<SisLabTopo.Domain.Exceptions.ServiceException>(() =>
            service.ConfigurarContrasenaInicialAsync("Otra-Clave-Cualquiera".ToCharArray()));

        Assert.Equal(SisLabTopo.Domain.Exceptions.ErrorCode.ContrasenaYaConfigurada, ex.Code);
    }

    [Fact]
    public async Task ConfigurarContrasenaInicial_GeneraCodigoDeRecuperacionYPermiteLoginConLaNuevaContrasena()
    {
        var service = CrearServicio();
        const string nueva = "Clave-Primer-Arranque-9";

        var codigo = await service.ConfigurarContrasenaInicialAsync(nueva.ToCharArray());

        Assert.False(string.IsNullOrWhiteSpace(codigo));
        Assert.Matches(@"^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$", codigo);
        // El alfabeto Crockford Base32 elegido no debe incluir caracteres ambiguos.
        Assert.DoesNotContain('I', codigo);
        Assert.DoesNotContain('L', codigo);
        Assert.DoesNotContain('O', codigo);
        Assert.DoesNotContain('U', codigo);

        var otraInstancia = CrearServicio();
        Assert.True(await otraInstancia.VerificarContrasenaAsync(nueva.ToCharArray()));
    }

    // ===================== Fase 6: recuperación de contraseña =====================

    [Fact]
    public async Task RestablecerContrasenaConCodigo_CodigoCorrecto_CambiaContrasenaYPreservaOtrosDatos()
    {
        var seedContext = CreateContext();
        _contexts.Add(seedContext);
        var equipoRepo = CreateEquipoRepo(seedContext);
        var configRepo = CreatePrestamoRepo(seedContext);

        var equipo = new SisLabTopo.Domain.Models.Equipo
        {
            Codigo = "EQ-FASE6-001",
            Denominacion = "Estación Total de Prueba",
            Disponible = true,
            FechaRegistro = DateTime.UtcNow,
        };
        await equipoRepo.GuardarAsync(equipo);
        await configRepo.GuardarConfigAsync(AuthService.ClaveNombreAdmin, "Ing. Prueba");

        var servicioInicial = new AuthService(configRepo, seedContext, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);
        var codigoOriginal = await servicioInicial.ConfigurarContrasenaInicialAsync("Clave-Original-123".ToCharArray());

        var service = CrearServicio();
        const string nuevaContrasena = "Clave-Restablecida-456";

        var nuevoCodigo = await service.RestablecerContrasenaConCodigoAsync(codigoOriginal.ToCharArray(), nuevaContrasena.ToCharArray());

        Assert.False(string.IsNullOrWhiteSpace(nuevoCodigo));

        var otraInstancia = CrearServicio();
        Assert.True(await otraInstancia.VerificarContrasenaAsync(nuevaContrasena.ToCharArray()));

        // El resto de los datos (equipos, config del nombre de admin) queda intacto --
        // a diferencia del "borre toda la base de datos" de la versión Java.
        var contextoVerificacion = CreateContext();
        _contexts.Add(contextoVerificacion);
        var equipoTrasReset = await CreateEquipoRepo(contextoVerificacion).BuscarPorCodigoAsync("EQ-FASE6-001");
        Assert.NotNull(equipoTrasReset);
        Assert.Equal("Estación Total de Prueba", equipoTrasReset!.Denominacion);

        var nombreAdminTrasReset = await CreatePrestamoRepo(contextoVerificacion).ObtenerConfigAsync(AuthService.ClaveNombreAdmin);
        Assert.Equal("Ing. Prueba", nombreAdminTrasReset);
    }

    [Fact]
    public async Task RestablecerContrasenaConCodigo_CodigoIncorrecto_NoCambiaNada()
    {
        var service = CrearServicio();
        var codigoOriginal = await service.ConfigurarContrasenaInicialAsync("Clave-Original-Segura".ToCharArray());

        var ex = await Assert.ThrowsAsync<SisLabTopo.Domain.Exceptions.ServiceException>(() =>
            service.RestablecerContrasenaConCodigoAsync("CODIGO-FALSO-0000".ToCharArray(), "Clave-Que-No-Deberia-Aplicarse".ToCharArray()));

        Assert.Equal(SisLabTopo.Domain.Exceptions.ErrorCode.CodigoRecuperacionInvalido, ex.Code);

        // La contraseña original sigue siendo válida; la "nueva" que se intentó
        // establecer con el código incorrecto NO debe funcionar.
        var otraInstancia = CrearServicio();
        Assert.True(await otraInstancia.VerificarContrasenaAsync("Clave-Original-Segura".ToCharArray()));

        var terceraInstancia = CrearServicio();
        Assert.False(await terceraInstancia.VerificarContrasenaAsync("Clave-Que-No-Deberia-Aplicarse".ToCharArray()));

        // El código original sigue vigente (no se invalidó por el intento fallido).
        var cuartaInstancia = CrearServicio();
        var nuevoCodigoConOriginal = await cuartaInstancia.RestablecerContrasenaConCodigoAsync(
            codigoOriginal.ToCharArray(), "Otra-Clave-Distinta-789".ToCharArray());
        Assert.False(string.IsNullOrWhiteSpace(nuevoCodigoConOriginal));
    }

    [Fact]
    public async Task RestablecerContrasenaConCodigo_DeberiaFallar_CuandoLaNuevaContrasenaEsDebil()
    {
        var service = CrearServicio();
        var codigoOriginal = await service.ConfigurarContrasenaInicialAsync("Clave-Original-Segura".ToCharArray());

        var otraInstancia = CrearServicio();
        var ex = await Assert.ThrowsAsync<SisLabTopo.Domain.Exceptions.ServiceException>(() =>
            otraInstancia.RestablecerContrasenaConCodigoAsync(codigoOriginal.ToCharArray(), "abc".ToCharArray()));

        Assert.Equal(SisLabTopo.Domain.Exceptions.ErrorCode.ContrasenaDebil, ex.Code);

        // Contraseña original sigue vigente.
        var terceraInstancia = CrearServicio();
        Assert.True(await terceraInstancia.VerificarContrasenaAsync("Clave-Original-Segura".ToCharArray()));
    }

    [Fact]
    public async Task RestablecerContrasenaConCodigo_GeneraUnCodigoNuevoDistintoDelAnterior_YElAnteriorYaNoSirve()
    {
        var service = CrearServicio();
        var codigoOriginal = await service.ConfigurarContrasenaInicialAsync("Clave-Original-Segura".ToCharArray());

        var instanciaReset = CrearServicio();
        var codigoNuevo = await instanciaReset.RestablecerContrasenaConCodigoAsync(
            codigoOriginal.ToCharArray(), "Clave-Tras-Reset-111".ToCharArray());

        Assert.NotEqual(codigoOriginal, codigoNuevo);

        // El código ANTERIOR ya no debe servir para un segundo restablecimiento.
        var instanciaIntentoConCodigoViejo = CrearServicio();
        var exConCodigoViejo = await Assert.ThrowsAsync<SisLabTopo.Domain.Exceptions.ServiceException>(() =>
            instanciaIntentoConCodigoViejo.RestablecerContrasenaConCodigoAsync(
                codigoOriginal.ToCharArray(), "Clave-Que-No-Deberia-Aplicar-222".ToCharArray()));
        Assert.Equal(SisLabTopo.Domain.Exceptions.ErrorCode.CodigoRecuperacionInvalido, exConCodigoViejo.Code);

        // El código NUEVO sí debe funcionar.
        var instanciaConCodigoNuevo = CrearServicio();
        var codigoFinal = await instanciaConCodigoNuevo.RestablecerContrasenaConCodigoAsync(
            codigoNuevo.ToCharArray(), "Clave-Tras-Segundo-Reset-333".ToCharArray());
        Assert.False(string.IsNullOrWhiteSpace(codigoFinal));
    }
}
