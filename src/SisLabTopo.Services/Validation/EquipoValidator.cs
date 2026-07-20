using FluentValidation;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Services.Validation;

/// <summary>
/// Validador de <see cref="Equipo"/>. Puerto 1:1 de las anotaciones Bean Validation de
/// <c>model.Equipo</c> (Java) — mismos campos, mismos mensajes en español, copiados
/// literalmente:
/// <list type="bullet">
/// <item><c>@NotBlank</c> en <c>codigo</c> → "El código no puede estar vacío"</item>
/// <item><c>@NotBlank</c> en <c>denominacion</c> → "La denominación no puede estar vacía"</item>
/// <item><c>@Size(max = 200)</c> en <c>denominacion</c> → "La denominación no puede superar los 200 caracteres"</item>
/// </list>
/// Nota de diseño: en Java, <c>estado</c>/<c>tipo</c> llevan <c>@NotNull</c> y
/// <c>fechaRegistro</c> lleva <c>@NotNull</c> porque esos campos son referencias que
/// podrían quedar sin asignar. En <see cref="Equipo"/> (C#) <c>Estado</c>/<c>Tipo</c> son
/// <c>enum</c> no-nulables (con valor por defecto <see cref="Domain.Enums.EstadoEquipoExtensions.ValorPorDefecto"/>/
/// <see cref="Domain.Enums.TipoEquipoExtensions.ValorPorDefecto"/>) y <c>FechaRegistro</c>
/// es un <c>DateTime</c> no-nulable: el compilador ya garantiza estructuralmente lo que
/// esas tres anotaciones Java verificaban en tiempo de ejecución, así que no se portan
/// como reglas de FluentValidation (no hay caso de prueba posible que las dispare).
/// </summary>
public class EquipoValidator : AbstractValidator<Equipo>
{
    public EquipoValidator()
    {
        RuleFor(e => e.Codigo)
            .NotEmpty().WithMessage("El código no puede estar vacío");

        RuleFor(e => e.Denominacion)
            .NotEmpty().WithMessage("La denominación no puede estar vacía")
            .MaximumLength(200).WithMessage("La denominación no puede superar los 200 caracteres");
    }
}
