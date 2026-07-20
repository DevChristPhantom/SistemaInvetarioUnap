namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// Fila de la mini-tabla "Equipos Más Prestados (Top 5)" del Dashboard: envuelve un
/// <see cref="Services.EquipoConteo"/> (ya ordenado desc. por <see cref="Services.IPrestamoService.EquiposMasPrestadosAsync"/>)
/// con la denominación del equipo YA resuelta, para no tener que consultar
/// <see cref="Services.IEquipoService"/> desde el binding/renderizado de cada celda
/// (mismo criterio anti-N+1 que <see cref="Prestamos.PrestamoRowViewModel"/>).
/// </summary>
public sealed record EquipoMasPrestadoRow(string Codigo, string Denominacion, int Cantidad);
