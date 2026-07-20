using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.UI.Prestamos;

/// <summary>Fila de equipo dentro del diálogo de detalle de préstamo (número, código, denominación).</summary>
public sealed record DetalleEquipoRow(int Numero, string Codigo, string Denominacion);

/// <summary>
/// ViewModel del diálogo "Ver Detalle" de un préstamo. Reemplaza el
/// <c>JOptionPane.showMessageDialog</c> de texto plano de <c>PrestamosPanel.java</c>
/// (<c>verDetallePrestamo</c>) por un diálogo real con los datos del préstamo y una
/// lista de sus equipos -- mejora de UX explícitamente permitida por el plan de
/// migración.
/// </summary>
public partial class DetallePrestamoViewModel : ObservableObject
{
    public Prestamo Prestamo { get; }

    public ObservableCollection<DetalleEquipoRow> Equipos { get; }

    public event EventHandler? SolicitarCierre;

    public DetallePrestamoViewModel(Prestamo prestamo, IReadOnlyList<DetalleEquipoRow> equipos)
    {
        Prestamo = prestamo;
        Equipos = new ObservableCollection<DetalleEquipoRow>(equipos);
    }

    [RelayCommand]
    private void Cerrar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
