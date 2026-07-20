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

    /// <summary>
    /// Indica si ya existe un hash de contraseña de administrador configurado. Usado por
    /// <c>App.OnStartup</c> (Fase 6) para decidir si mostrar el asistente de primer
    /// arranque (<c>false</c>) o el login normal (<c>true</c>).
    /// </summary>
    Task<bool> ExisteContrasenaConfiguradaAsync(CancellationToken ct = default);

    /// <summary>
    /// Asistente de primer arranque: define la contraseña de administrador inicial y
    /// genera un código de recuperación nuevo. Solo debe invocarse cuando
    /// <see cref="ExisteContrasenaConfiguradaAsync"/> es <c>false</c> -- lanza
    /// <see cref="Domain.Exceptions.ServiceException"/> (<c>ContrasenaYaConfigurada</c>)
    /// si ya había una contraseña configurada, y (<c>ContrasenaDebil</c>) si
    /// <paramref name="nuevaContrasena"/> tiene menos de 6 caracteres. Devuelve el código
    /// de recuperación en texto plano -- ÚNICA vez que existe en memoria fuera de este
    /// método; solo su hash BCrypt se persiste. Limpia <paramref name="nuevaContrasena"/>
    /// en memoria antes de retornar.
    /// </summary>
    Task<string> ConfigurarContrasenaInicialAsync(char[] nuevaContrasena, CancellationToken ct = default);

    /// <summary>
    /// Restablece la contraseña de administrador usando el código de recuperación vigente
    /// (reemplaza el "edite la celda de Excel a mano o borre la base de datos completa"
    /// de la versión Java): no destruye ningún otro dato (equipos/préstamos/historial
    /// quedan intactos). Lanza <see cref="Domain.Exceptions.ServiceException"/>
    /// (<c>CodigoRecuperacionInvalido</c>) si <paramref name="codigoRecuperacion"/> no
    /// coincide con el hash guardado, o (<c>ContrasenaDebil</c>) si
    /// <paramref name="nuevaContrasena"/> tiene menos de 6 caracteres -- en ambos casos no
    /// cambia nada. Si tiene éxito, además de actualizar la contraseña genera y persiste
    /// un CÓDIGO DE RECUPERACIÓN NUEVO (invalida el anterior) y limpia cualquier bloqueo
    /// de intentos fallidos vigente. Devuelve el nuevo código en texto plano (única vez
    /// visible). Limpia ambos buffers en memoria antes de retornar.
    /// </summary>
    Task<string> RestablecerContrasenaConCodigoAsync(char[] codigoRecuperacion, char[] nuevaContrasena, CancellationToken ct = default);
}
