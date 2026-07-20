namespace SisLabTopo.Domain.Exceptions;

/// <summary>
/// Códigos de error específicos del negocio para identificar fallas en el sistema.
/// Puerto 1:1 (mismos 12 valores, mismo orden) de <c>exception.ErrorCode</c> (Java),
/// renombrados de SCREAMING_SNAKE_CASE a PascalCase por convención C#.
/// </summary>
public enum ErrorCode
{
    /// <summary>Java: EQUIPO_NO_ENCONTRADO</summary>
    EquipoNoEncontrado,

    /// <summary>Java: EQUIPO_NO_DISPONIBLE</summary>
    EquipoNoDisponible,

    /// <summary>Java: EQUIPO_EN_PRESTAMO_ACTIVO</summary>
    EquipoEnPrestamoActivo,

    /// <summary>Java: PRESTAMO_NO_ENCONTRADO</summary>
    PrestamoNoEncontrado,

    /// <summary>Java: PRESTAMO_YA_DEVUELTO</summary>
    PrestamoYaDevuelto,

    /// <summary>Java: CREDENCIALES_INCORRECTAS</summary>
    CredencialesIncorrectas,

    /// <summary>Java: CONTRASENA_DEBIL</summary>
    ContrasenaDebil,

    /// <summary>Java: LIMITE_INTENTOS_ALCANZADO</summary>
    LimiteIntentosAlcanzado,

    /// <summary>Java: ERROR_LECTURA_BASE_DATOS</summary>
    ErrorLecturaBaseDatos,

    /// <summary>Java: ERROR_ESCRITURA_BASE_DATOS</summary>
    ErrorEscrituraBaseDatos,

    /// <summary>Java: DATOS_INVALIDOS</summary>
    DatosInvalidos,

    /// <summary>Java: ARCHIVO_EXCEL_INVALIDO</summary>
    ArchivoExcelInvalido
}
