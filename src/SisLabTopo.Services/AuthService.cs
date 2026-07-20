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

    /// <summary>
    /// Clave en <c>ConfigEntries</c> donde se guarda el hash BCrypt del código de
    /// recuperación vigente (Fase 6). El código en texto plano NUNCA se persiste -- solo
    /// existe en memoria durante el asistente de primer arranque / el flujo de
    /// recuperación, el tiempo justo para mostrarlo una vez al administrador.
    /// </summary>
    public const string ClaveHashCodigoRecuperacion = "admin.recovery.codigo.hash";

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

    public async Task<bool> ExisteContrasenaConfiguradaAsync(CancellationToken ct = default)
    {
        var hash = await _configRepo.ObtenerConfigAsync(ClaveHashContrasena, ct);
        return !string.IsNullOrEmpty(hash);
    }

    public async Task<string> ConfigurarContrasenaInicialAsync(char[] nuevaContrasena, CancellationToken ct = default)
    {
        try
        {
            if (nuevaContrasena is null || nuevaContrasena.Length < 6)
            {
                throw new Domain.Exceptions.ServiceException(
                    Domain.Exceptions.ErrorCode.ContrasenaDebil,
                    "La contraseña debe tener al menos 6 caracteres.");
            }

            var hashExistente = await _configRepo.ObtenerConfigAsync(ClaveHashContrasena, ct);
            if (!string.IsNullOrEmpty(hashExistente))
            {
                throw new Domain.Exceptions.ServiceException(
                    Domain.Exceptions.ErrorCode.ContrasenaYaConfigurada,
                    "Ya existe una contraseña de administrador configurada.");
            }

            var hashContrasena = BCrypt.Net.BCrypt.HashPassword(new string(nuevaContrasena), workFactor: CostoBCrypt);
            await _configRepo.GuardarConfigAsync(ClaveHashContrasena, hashContrasena, ct);

            var codigoRecuperacion = GenerarCodigoRecuperacion();
            var hashCodigo = BCrypt.Net.BCrypt.HashPassword(codigoRecuperacion, workFactor: CostoBCrypt);
            await _configRepo.GuardarConfigAsync(ClaveHashCodigoRecuperacion, hashCodigo, ct);

            _logger.LogInformation(
                "Asistente de primer arranque: contraseña de administrador configurada y código de recuperación generado.");
            return codigoRecuperacion;
        }
        finally
        {
            Array.Clear(nuevaContrasena, 0, nuevaContrasena.Length);
        }
    }

    public async Task<string> RestablecerContrasenaConCodigoAsync(char[] codigoRecuperacion, char[] nuevaContrasena, CancellationToken ct = default)
    {
        try
        {
            if (nuevaContrasena is null || nuevaContrasena.Length < 6)
            {
                throw new Domain.Exceptions.ServiceException(
                    Domain.Exceptions.ErrorCode.ContrasenaDebil,
                    "La nueva contraseña debe tener al menos 6 caracteres.");
            }

            var hashCodigoActual = await _configRepo.ObtenerConfigAsync(ClaveHashCodigoRecuperacion, ct);
            var codigoValido = !string.IsNullOrEmpty(hashCodigoActual)
                && codigoRecuperacion is not null
                && codigoRecuperacion.Length > 0
                && BCrypt.Net.BCrypt.Verify(new string(codigoRecuperacion), hashCodigoActual);

            if (!codigoValido)
            {
                _logger.LogWarning("Intento de recuperación de contraseña con un código de recuperación inválido.");
                throw new Domain.Exceptions.ServiceException(
                    Domain.Exceptions.ErrorCode.CodigoRecuperacionInvalido,
                    "El código de recuperación ingresado no es válido.");
            }

            var nuevoHashContrasena = BCrypt.Net.BCrypt.HashPassword(new string(nuevaContrasena), workFactor: CostoBCrypt);
            await _configRepo.GuardarConfigAsync(ClaveHashContrasena, nuevoHashContrasena, ct);

            // El código usado queda invalidado: se genera y persiste uno nuevo (nunca se
            // reutiliza el mismo), igual que un código de recuperación de un solo uso.
            var nuevoCodigo = GenerarCodigoRecuperacion();
            var nuevoHashCodigo = BCrypt.Net.BCrypt.HashPassword(nuevoCodigo, workFactor: CostoBCrypt);
            await _configRepo.GuardarConfigAsync(ClaveHashCodigoRecuperacion, nuevoHashCodigo, ct);

            // Quien demuestra poseer el código de recuperación ya demostró ser el
            // administrador legítimo: no tiene sentido dejar vigente un bloqueo de
            // intentos fallidos previo.
            await ResetearIntentosAsync(ct);

            _logger.LogInformation(
                "Contraseña de administrador restablecida vía código de recuperación. Se generó un nuevo código de recuperación (el anterior queda invalidado).");
            return nuevoCodigo;
        }
        finally
        {
            if (codigoRecuperacion is not null)
            {
                Array.Clear(codigoRecuperacion, 0, codigoRecuperacion.Length);
            }

            Array.Clear(nuevaContrasena, 0, nuevaContrasena.Length);
        }
    }

    /// <summary>
    /// Genera un código de recuperación aleatorio criptográficamente seguro, formato
    /// "XXXX-XXXX-XXXX-XXXX" (16 caracteres + separadores) usando el alfabeto de
    /// Crockford Base32 (excluye I, L, O, U para evitar ambigüedad visual con 1/0/V) --
    /// suficientemente largo para no ser adivinable por fuerza bruta, pero razonable de
    /// transcribir/copiar a mano.
    /// </summary>
    private static string GenerarCodigoRecuperacion()
    {
        const string alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        const int totalCaracteres = 16;

        Span<byte> buffer = stackalloc byte[totalCaracteres];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);

        var chars = new char[totalCaracteres];
        for (var i = 0; i < totalCaracteres; i++)
        {
            chars[i] = alfabeto[buffer[i] % alfabeto.Length];
        }

        var grupos = new string[totalCaracteres / 4];
        for (var g = 0; g < grupos.Length; g++)
        {
            grupos[g] = new string(chars, g * 4, 4);
        }

        return string.Join("-", grupos);
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
