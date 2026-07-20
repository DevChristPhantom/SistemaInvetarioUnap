using FluentValidation;
using SisLabTopo.Domain.Exceptions;

namespace SisLabTopo.Services.Validation;

/// <summary>
/// Equivalente a <c>util.ValidationUtils.validar()</c> (Java): ejecuta un validador de
/// FluentValidation y, si hay violaciones, junta todos los mensajes con <c>\n</c> y
/// lanza una única <see cref="ServiceException"/> con <see cref="ErrorCode.DatosInvalidos"/>
/// — igual que Java junta los mensajes de <c>ConstraintViolation</c> con
/// <c>Collectors.joining("\n")</c>.
/// </summary>
public static class ValidationHelper
{
    public static void Validar<T>(IValidator<T> validador, T objeto)
    {
        var resultado = validador.Validate(objeto);
        if (!resultado.IsValid)
        {
            var mensaje = string.Join("\n", resultado.Errors.Select(e => e.ErrorMessage));
            throw new ServiceException(ErrorCode.DatosInvalidos, mensaje);
        }
    }
}
