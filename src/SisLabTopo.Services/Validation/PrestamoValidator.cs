using FluentValidation;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Services.Validation;

/// <summary>
/// Validador de <see cref="Prestamo"/>. Puerto 1:1 de las anotaciones Bean Validation de
/// <c>model.Prestamo</c> (Java) — mismos campos, mismos mensajes en español, copiados
/// literalmente:
/// <list type="bullet">
/// <item><c>@NotBlank</c> en <c>id</c> → "El ID del préstamo no puede estar vacío"</item>
/// <item><c>@NotBlank</c> en <c>docente</c> → "El docente responsable es obligatorio"</item>
/// <item><c>@NotBlank</c> en <c>curso</c> → "El curso es obligatorio"</item>
/// <item><c>@NotBlank</c> en <c>semestre</c> → "El semestre es obligatorio"</item>
/// <item><c>@NotBlank</c> en <c>nombreEstudiante</c> → "El nombre del estudiante es obligatorio"</item>
/// <item><c>@NotBlank</c> en <c>codigoEstudiante</c> → "El código del estudiante es obligatorio"</item>
/// </list>
/// Igual que en <see cref="EquipoValidator"/>: <c>@NotNull</c> en <c>fechaPrestamo</c>,
/// <c>estado</c> y <c>fechaRegistro</c> no se portan porque <see cref="Prestamo"/> (C#)
/// los modela como tipos de valor no-nulables (<c>DateTime</c>/<c>enum</c>), así que el
/// compilador ya garantiza esa invariante. <c>observaciones</c> no lleva ninguna
/// anotación en Java (es opcional) y tampoco tiene regla aquí.
/// </summary>
public class PrestamoValidator : AbstractValidator<Prestamo>
{
    public PrestamoValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty().WithMessage("El ID del préstamo no puede estar vacío");

        RuleFor(p => p.Docente)
            .NotEmpty().WithMessage("El docente responsable es obligatorio");

        RuleFor(p => p.Curso)
            .NotEmpty().WithMessage("El curso es obligatorio");

        RuleFor(p => p.Semestre)
            .NotEmpty().WithMessage("El semestre es obligatorio");

        RuleFor(p => p.NombreEstudiante)
            .NotEmpty().WithMessage("El nombre del estudiante es obligatorio");

        RuleFor(p => p.CodigoEstudiante)
            .NotEmpty().WithMessage("El código del estudiante es obligatorio");
    }
}
