namespace SisLabTopo.Domain.Exceptions;

/// <summary>
/// Excepción lanzada al ocurrir un fallo en la generación o impresión de comprobantes.
/// Puerto 1:1 de <c>exception.PrintException</c> (Java).
/// </summary>
public class PrintException : Exception
{
    public PrintException(string message)
        : base(message)
    {
    }

    public PrintException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
