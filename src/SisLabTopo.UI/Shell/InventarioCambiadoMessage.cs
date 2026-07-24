namespace SisLabTopo.UI.Shell;

/// <summary>
/// Mensaje vacío (CommunityToolkit.Mvvm.Messaging) enviado cada vez que cambia algo que
/// afecta la disponibilidad de equipos: alta/edición/eliminación/importación de equipos
/// (<c>EquiposViewModel</c>) o creación/devolución de un préstamo (<c>PrestamosViewModel</c>).
///
/// Corrige un bug real encontrado en QA manual: la barra de estado inferior del Shell
/// ("Equipos Disponibles: X de Y") solo se refrescaba al navegar entre pantallas
/// (<see cref="ShellViewModel.ActualizarBarraEstadoAsync"/>, llamado desde
/// <c>Navegar&lt;T&gt;</c>) -- si el usuario completaba un préstamo de 6 equipos sin
/// salir de la pantalla de Préstamos, la barra seguía mostrando el conteo previo
/// ("6 de 6" en vez de "0 de 6") hasta que navegaba a otra pantalla y volvía.
///
/// Se usa <c>WeakReferenceMessenger.Default</c> (ya incluido con CommunityToolkit.Mvvm,
/// sin registro nuevo en el contenedor de DI) en vez de inyectar una dependencia directa
/// entre EquiposViewModel/PrestamosViewModel y ShellViewModel: son Transient con
/// ciclos de vida independientes (uno por navegación) y no hay ninguna razón de negocio
/// para que se conozcan entre sí más allá de este aviso puntual.
/// </summary>
public sealed class InventarioCambiadoMessage;
