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
}
