namespace SisLabTopo.Domain.Exceptions;

/// <summary>
/// Excepción de negocio lanzada por los servicios y repositorios del sistema.
/// Puerto 1:1 de <c>exception.ServiceException</c> (Java).
/// </summary>
public class ServiceException : Exception
{
    public ErrorCode Code { get; }

    public ServiceException(ErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public ServiceException(ErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
