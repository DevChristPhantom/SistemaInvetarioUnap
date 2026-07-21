using SisLabTopo.Domain.Models;

namespace SisLabTopo.UI.Dashboard;

/// <summary>
/// Fila del timeline visual de "Últimos Préstamos Activos" (fila 3 del Dashboard,
/// Fase B). Envuelve el <see cref="Prestamo"/> tal cual (para poder bindear
/// <c>Prestamo.Estado</c> directamente a <see cref="Converters.EstadoPrestamoToBrushConverter"/>
/// y así colorear el punto del timeline exactamente igual que cualquier
/// <see cref="Controls.StatusBadge"/> de la app) + un resumen legible del/los equipo(s)
/// prestados en esa transacción.
/// </summary>
public sealed record PrestamoTimelineItem(Prestamo Prestamo, string EquipoResumen);
