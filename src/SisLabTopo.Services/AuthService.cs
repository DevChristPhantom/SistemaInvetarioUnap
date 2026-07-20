using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SisLabTopo.Data;
using SisLabTopo.Data.Entities;
using SisLabTopo.Data.Repositories;

namespace SisLabTopo.Services;

/// <inheritdoc cref="IAuthService"/>
public class AuthService : IAuthService
{
    /// <summary>Clave en <c>ConfigEntries</c> donde se guarda el hash BCrypt de la contraseña de administrador.</summary>
    public const string ClaveHashContrasena = "admin.password.hash";

    /// <summary>Clave en <c>ConfigEntries</c> donde se guarda el nombre para mostrar del administrador.</summary>
    public const string ClaveNombreAdmin = "admin.nombre";

    private const int CostoBCrypt = 12;
    private const int MaxIntentosFallidos = 3;
    private const int SegundosBloqueo = 30;

    private readonly IPrestamoRepository _configRepo;
    private readonly SisLabTopoDbContext _context;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Nota de diseño: <paramref name="configRepo"/> se usa (igual que en Java, que
    /// reutiliza <c>PrestamoRepository.obtenerConfig/guardarConfig</c>) para leer/escribir
    /// los pares clave/valor de <c>ConfigEntries</c> (hash de contraseña, nombre de
    /// administrador). El estado runtime de bloqueo (<see cref="AppState"/>) no forma
    /// parte de ningún contrato de repositorio — por diseño explícito de la Fase 1 (ver
    /// el comentario XML de <see cref="AppState"/>: "que la Fase 2 (AuthService) leerá y
    /// escribirá directamente") — así que aquí se inyecta el <see cref="SisLabTopoDbContext"/>
    /// directamente para esa única tabla singleton.
    /// </summary>
    public AuthService(IPrestamoRepository configRepo, SisLabTopoDbContext context, ILogger<AuthService> logger)
    {
        _configRepo = configRepo;
        _context = context;
        _logger = logger;
    }

    public async Task<bool> VerificarContrasenaAsync(char[] contrasena, CancellationToken ct = default)
    {
        try
        {
            var appState = await ObtenerOCrearAppStateAsync(ct);

            if (await SegundosBloqueoRestantesAsync(appState, ct) > 0)
            {
                _logger.LogWarning("Intento de acceso rechazado: la cuenta se encuentra bloqueada.");
                return false;
            }

            var hash = await _configRepo.ObtenerConfigAsync(ClaveHashContrasena, ct);
            if (string.IsNullOrEmpty(hash))
            {
                // Sin contraseña configurada todavía (esperado antes del asistente de
                // primer arranque de la Fase 6): se trata como acceso denegado, sin
                // contar como intento fallido y sin hardcodear ninguna contraseña por
                // defecto, igual que hace Java cuando el hash está vacío.
                _logger.LogError("No se encontró el hash de contraseña de administrador en la configuración.");
                return false;
            }

            var coincide = BCrypt.Net.BCrypt.Verify(new string(contrasena), hash);
            if (coincide)
            {
                appState.IntentosFallidos = 0;
                appState.HoraBloqueoUtc = null;
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Acceso administrativo concedido.");
                return true;
            }

            appState.IntentosFallidos++;
            _logger.LogWarning("Intento fallido de inicio de sesión ({Intentos} de {Max}).", appState.IntentosFallidos, MaxIntentosFallidos);
            if (appState.IntentosFallidos >= MaxIntentosFallidos)
            {
                appState.HoraBloqueoUtc = DateTime.UtcNow.AddSeconds(SegundosBloqueo);
                _logger.LogWarning("Se ha alcanzado el límite de intentos. Cuenta bloqueada hasta: {HoraBloqueo}", appState.HoraBloqueoUtc);
            }

            await _context.SaveChangesAsync(ct);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar la contraseña del administrador.");
            return false;
        }
        finally
        {
            Array.Clear(contrasena, 0, contrasena.Length); // limpieza de seguridad, equivalente a Arrays.fill en Java
        }
    }

    public async Task CambiarContrasenaAsync(char[] actual, char[] nueva, CancellationToken ct = default)
    {
        try
        {
            if (nueva is null || nueva.Length < 6)
            {
                throw new Domain.Exceptions.ServiceException(
                    Domain.Exceptions.ErrorCode.ContrasenaDebil,
                    "La nueva contraseña debe tener al menos 6 caracteres.");
            }

            var hashActual = await _configRepo.ObtenerConfigAsync(ClaveHashContrasena, ct);
            if (string.IsNullOrEmpty(hashActual) || !BCrypt.Net.BCrypt.Verify(new string(actual), hashActual))
            {
                throw new Domain.Exceptions.ServiceException(
                    Domain.Exceptions.ErrorCode.CredencialesIncorrectas,
                    "La contraseña actual ingresada es incorrecta.");
            }

            var nuevoHash = BCrypt.Net.BCrypt.HashPassword(new string(nueva), workFactor: CostoBCrypt);
            await _configRepo.GuardarConfigAsync(ClaveHashContrasena, nuevoHash, ct);
            _logger.LogInformation("Contraseña de administrador cambiada exitosamente.");
        }
        finally
        {
            Array.Clear(actual, 0, actual.Length);
            Array.Clear(nueva, 0, nueva.Length);
        }
    }

    public async Task<string> ObtenerNombreAdminAsync(CancellationToken ct = default)
    {
        try
        {
            var nombre = await _configRepo.ObtenerConfigAsync(ClaveNombreAdmin, ct);
            return string.IsNullOrEmpty(nombre) ? "Administrador" : nombre;
        }
        catch (Exception)
        {
            return "Administrador";
        }
    }

    public async Task<long> ObtenerSegundosBloqueoAsync(CancellationToken ct = default)
    {
        var appState = await ObtenerOCrearAppStateAsync(ct);
        return await SegundosBloqueoRestantesAsync(appState, ct);
    }

    public async Task<int> ObtenerIntentosFallidosAsync(CancellationToken ct = default)
    {
        var appState = await ObtenerOCrearAppStateAsync(ct);
        return appState.IntentosFallidos;
    }

    public async Task ResetearIntentosAsync(CancellationToken ct = default)
    {
        var appState = await ObtenerOCrearAppStateAsync(ct);
        appState.IntentosFallidos = 0;
        appState.HoraBloqueoUtc = null;
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Calcula los segundos de bloqueo restantes a partir del <see cref="AppState"/>
    /// dado. Si el bloqueo ya expiró, lo limpia (intentos + hora de bloqueo) y persiste
    /// ese reseteo — efecto colateral idéntico al de <c>obtenerSegundosBloqueo()</c> en
    /// Java.
    /// </summary>
    private async Task<long> SegundosBloqueoRestantesAsync(AppState appState, CancellationToken ct)
    {
        if (appState.HoraBloqueoUtc is null)
        {
            return 0;
        }

        var ahora = DateTime.UtcNow;
        if (ahora > appState.HoraBloqueoUtc.Value)
        {
            appState.HoraBloqueoUtc = null;
            appState.IntentosFallidos = 0;
            await _context.SaveChangesAsync(ct);
            return 0;
        }

        return (long)Math.Ceiling((appState.HoraBloqueoUtc.Value - ahora).TotalSeconds);
    }

    private async Task<AppState> ObtenerOCrearAppStateAsync(CancellationToken ct)
    {
        var appState = await _context.AppStates.FirstOrDefaultAsync(a => a.Id == 1, ct);
        if (appState is null)
        {
            appState = new AppState { Id = 1 };
            _context.AppStates.Add(appState);
            await _context.SaveChangesAsync(ct);
        }

        return appState;
    }
}
