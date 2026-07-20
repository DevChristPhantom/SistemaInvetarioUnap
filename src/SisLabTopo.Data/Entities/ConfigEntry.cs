namespace SisLabTopo.Data.Entities;

/// <summary>
/// Par clave/valor de configuración de la aplicación. Equivalente a la hoja
/// <c>CONFIG</c> del Excel legado (admin.nombre, admin.password.hash,
/// institucion.nombre, etc.). No es parte del dominio de negocio portado desde
/// Java 1:1 — es un concepto propio de la capa de persistencia, por eso vive en
/// <c>SisLabTopo.Data</c> y no en <c>SisLabTopo.Domain</c>.
/// </summary>
public class ConfigEntry
{
    public string Clave { get; set; } = string.Empty;

    public string? Valor { get; set; }
}
