namespace SisLabTopo.Data.Entities;

/// <summary>
/// Estado runtime persistido de la aplicación, fila única (singleton, <see cref="Id"/> = 1).
/// Hoy en Java el contador de intentos fallidos de login y el bloqueo temporal viven
/// solo en memoria (<c>AuthServiceImpl</c>) y se pierden al reiniciar la app — un bug
/// de seguridad real, señalado en el plan de migración. Aquí se modela como columnas
/// tipadas dedicadas (no como pares clave/valor genéricos de <see cref="ConfigEntry"/>)
/// porque son datos estructurados con tipo propio (int, DateTime?) que la Fase 2
/// (AuthService) leerá y escribirá directamente.
/// </summary>
public class AppState
{
    public int Id { get; set; } = 1;

    public int IntentosFallidos { get; set; }

    public DateTime? HoraBloqueoUtc { get; set; }
}
