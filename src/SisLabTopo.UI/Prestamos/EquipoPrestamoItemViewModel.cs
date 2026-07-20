using CommunityToolkit.Mvvm.ComponentModel;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// Una de las 6 filas fijas de equipo del formulario de Nuevo Préstamo. Puerto funcional
/// de la clase interna <c>NuevoPrestamoDialog.EquipmentRow</c> (Java): Denominación/
/// Código/Estado son de solo lectura (se llenan al elegir un equipo desde el buscador),
/// con botones "Buscar"/"Quitar" en la vista.
/// </summary>
public partial class EquipoPrestamoItemViewModel : ObservableObject
{
    /// <summary>Número de fila (1-6), mostrado en la columna "#" -- puramente informativo.</summary>
    public int Numero { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneEquipo))]
    private string _codigo = string.Empty;

    [ObservableProperty]
    private string _denominacion = string.Empty;

    [ObservableProperty]
    private string _estado = string.Empty;

    public bool TieneEquipo => !string.IsNullOrEmpty(Codigo);

    public EquipoPrestamoItemViewModel(int numero)
    {
        Numero = numero;
    }

    public void Asignar(Equipo equipo)
    {
        Codigo = equipo.Codigo;
        Denominacion = equipo.Denominacion;
        Estado = equipo.Estado.Etiqueta();
    }

    public void Limpiar()
    {
        Codigo = string.Empty;
        Denominacion = string.Empty;
        Estado = string.Empty;
    }
}
