namespace SisLabTopo.UI.Equipos;

/// <summary>
/// Ítem de un combo de filtro que agrega una opción "Todos"/"Todas" (representada por
/// <see cref="Valor"/> = <c>null</c>) por delante de todos los valores de un enum. Puerto
/// del patrón que <c>EquiposPanel.java</c> lograba con un <c>DefaultListCellRenderer</c>
/// personalizado sobre un <c>JComboBox</c> cuyo primer ítem era literalmente <c>null</c>
/// (<c>cbEstado.addItem(null)</c>); aquí se modela explícitamente como un valor de
/// opción en vez de depender de que el enlace de datos tolere <c>null</c> en el
/// <c>ComboBox</c>.
/// </summary>
public sealed record FiltroOpcion<TEnum>(string Etiqueta, TEnum? Valor) where TEnum : struct, Enum;
