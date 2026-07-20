namespace SisLabTopo.Services;

/// <summary>
/// Servicio de autenticación y seguridad del administrador único del sistema.
/// Puerto 1:1 de <c>service.AuthService</c> (Java), con nombres async por convención
/// idiomática de C#.
///
/// Diferencia deliberada con Java (mejora de seguridad acordada en el plan de
/// migración): el contador de intentos fallidos y la hora de bloqueo YA NO viven solo
/// en memoria de instancia — se persisten en <c>SisLabTopo.Data.Entities.AppState</c>,
/// así que dos instancias de <see cref="AuthService"/> construidas sobre la misma base
/// de datos comparten el mismo estado de bloqueo, y ese estado sobrevive a un reinicio
/// de la aplicación.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Verifica la contraseña de administrador ingresada contra el hash BCrypt
    /// almacenado. Devuelve <c>false</c> si la cuenta está bloqueada (incluso si la
    /// contraseña es correcta), si no coincide, o si no hay ningún hash configurado
    /// todavía. Limpia el buffer <paramref name="contrasena"/> en memoria antes de
    /// retornar (éxito, fallo o excepción).
    /// </summary>
    Task<bool> VerificarContrasenaAsync(char[] contrasena, CancellationToken ct = default);

    /// <summary>
    /// Cambia la contraseña de administrador. Exige que <paramref name="actual"/>
    /// coincida con el hash almacenado y que <paramref name="nueva"/> tenga al menos 6
    /// caracteres. Limpia ambos buffers en memoria antes de retornar.
    /// </summary>
    Task CambiarContrasenaAsync(char[] actual, char[] nueva, CancellationToken ct = default);

    /// <summary>Nombre para mostrar del administrador (o "Administrador" si no está configurado).</summary>
    Task<string> ObtenerNombreAdminAsync(CancellationToken ct = default);

    /// <summary>
    /// Segundos restantes de bloqueo (0 si no hay bloqueo vigente). Si el bloqueo ya
    /// expiró, limpia el estado de bloqueo persistido como efecto colateral (igual que
    /// la versión Java).
    /// </summary>
    Task<long> ObtenerSegundosBloqueoAsync(CancellationToken ct = default);

    /// <summary>Cantidad de intentos fallidos consecutivos registrados hasta ahora.</summary>
    Task<int> ObtenerIntentosFallidosAsync(CancellationToken ct = default);

    /// <summary>Limpia manualmente el contador de intentos fallidos y cualquier bloqueo vigente.</summary>
    Task ResetearIntentosAsync(CancellationToken ct = default);
}
