using SisLabTopo.Domain.Models;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// Fila de la tabla de préstamos activos: envuelve un <see cref="Domain.Models.Prestamo"/>
/// con datos ya resueltos para no tener que consultar el servicio desde el
/// binding/renderizado de cada celda (la causa raíz del problema N+1 de
/// <c>PrestamosPanel.java</c>, que llamaba <c>service.obtenerDetalle(p.getId())</c> por
/// cada fila EN CADA repintado de la tabla). <see cref="PrestamosViewModel"/> calcula
/// <see cref="CantidadEquipos"/> una sola vez por recarga (ver comentario en
/// <c>PrestamosViewModel.CargarAsync</c>).
/// </summary>
public sealed class PrestamoRowViewModel
{
    public Prestamo Prestamo { get; }

    public int CantidadEquipos { get; }

    /// <summary>Primeros 8 caracteres del GUID, igual que <c>p.getId().substring(0, 8)</c> en Java.</summary>
    public string IdCorto => Prestamo.Id.Length > 8 ? Prestamo.Id[..8] : Prestamo.Id;

    public PrestamoRowViewModel(Prestamo prestamo, int cantidadEquipos)
    {
        Prestamo = prestamo;
        CantidadEquipos = cantidadEquipos;
    }
}
