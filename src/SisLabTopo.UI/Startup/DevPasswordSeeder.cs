using Microsoft.Extensions.Logging;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Services;

namespace SisLabTopo.UI.Startup;

/// <summary>
/// Siembra TEMPORAL, exclusiva de builds DEBUG, de una contraseña de administrador
/// aleatoria para poder ejercitar el flujo de login de extremo a extremo en esta fase.
///
/// Contexto: desde la Fase 2, <c>AuthService</c> ya NO trae ninguna contraseña por
/// defecto hardcodeada (se quitó deliberadamente el "admin123" que traía el instalador
/// Java en texto plano -- ese era, de hecho, uno de los huecos de seguridad que motivó
/// la migración). Eso significa que, hasta que exista el asistente de primer arranque
/// de la Fase 6, no hay ninguna forma de iniciar sesión en una base de datos nueva. En
/// vez de reintroducir una contraseña fija conocida (que repetiría exactamente el mismo
/// hueco que se corrigió), esta clase genera una contraseña aleatoria una única vez (si
/// todavía no hay ningún hash guardado), la guarda con el mismo hash BCrypt que usará
/// el flujo real, y la escribe en el log de arranque (Serilog) y en la consola -- SOLO
/// en compilaciones DEBUG. En Release, esta clase no se compila (ver <c>#if DEBUG</c>
/// en <see cref="App"/>) y una base de datos nueva sencillamente no tiene contraseña
/// configurada todavía, tal como corresponde antes de que exista el asistente de primer
/// arranque.
///
/// TODO Fase 6: esto se reemplaza por el asistente de primer arranque (definir
/// contraseña de administrador obligatoriamente antes de poder usar la app).
/// </summary>
public class DevPasswordSeeder
{
    private readonly IPrestamoRepository _configRepo;
    private readonly ILogger<DevPasswordSeeder> _logger;

    public DevPasswordSeeder(IPrestamoRepository configRepo, ILogger<DevPasswordSeeder> logger)
    {
        _configRepo = configRepo;
        _logger = logger;
    }

    public async Task SembrarSiHaceFaltaAsync(CancellationToken ct = default)
    {
        var hashExistente = await _configRepo.ObtenerConfigAsync(AuthService.ClaveHashContrasena, ct);
        if (!string.IsNullOrEmpty(hashExistente))
        {
            return;
        }

        var passwordGenerada = GenerarContrasenaAleatoria();
        var hash = BCrypt.Net.BCrypt.HashPassword(passwordGenerada, workFactor: 12);
        await _configRepo.GuardarConfigAsync(AuthService.ClaveHashContrasena, hash, ct);

        _logger.LogWarning(
            "[SOLO DEBUG] No había contraseña de administrador configurada. Se generó una " +
            "temporal para poder probar el login: {Password} -- este mecanismo se reemplaza " +
            "por el asistente de primer arranque en la Fase 6.", passwordGenerada);

        Console.WriteLine("==============================================================");
        Console.WriteLine(" [DEV] Contraseña de administrador temporal generada:");
        Console.WriteLine($"       {passwordGenerada}");
        Console.WriteLine(" (Solo en builds DEBUG -- ver DevPasswordSeeder. TODO Fase 6.)");
        Console.WriteLine("==============================================================");
    }

    private static string GenerarContrasenaAleatoria()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        Span<byte> buffer = stackalloc byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);

        var chars = new char[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
        {
            chars[i] = alfabeto[buffer[i] % alfabeto.Length];
        }

        return new string(chars);
    }
}
