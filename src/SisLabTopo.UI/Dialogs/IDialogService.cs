using System.Windows;

namespace SisLabTopo.UI.Dialogs;

/// <summary>
/// Abstracción de "mostrar un diálogo modal" para que los ViewModels de Fase 5
/// (formulario de equipo, nuevo préstamo, devolución, vista previa de comprobante,
/// búsqueda de equipo, detalle de préstamo) puedan pedir que se abra otro diálogo sin
/// depender directamente de WPF (<see cref="Window"/>) ni del contenedor de DI --
/// exactamente el mismo motivo por el que <see cref="Navigation.INavigationService"/>
/// existe para la navegación de ventana raíz/Shell (Fase 4).
///
/// <para>
/// <b>Patrón elegido (documentado para la siguiente fase):</b> cada diálogo es una
/// <see cref="Window"/> real resuelta vía DI (registrada en
/// <c>ServiceRegistration.AddSisLabTopoUi</c>), mapeada explícitamente por el tipo
/// concreto de su ViewModel en el diccionario que recibe <see cref="DialogService"/>.
/// El ViewModel del diálogo (p.ej. <c>EquipoFormViewModel</c>) se construye
/// DIRECTAMENTE con <c>new</c> por el ViewModel que lo abre (pasándole los mismos
/// servicios que ya tiene inyectados, p.ej. <c>IEquipoService</c>), no a través del
/// contenedor de DI -- así el ViewModel del diálogo queda 100% testeable con Moq (se
/// puede instanciar en un test sin ninguna infraestructura de WPF), y
/// <see cref="DialogService"/> solo necesita saber "qué ventana le corresponde a qué
/// tipo de ViewModel" para poder mostrarla. La alternativa (Window con code-behind
/// mínimo + DataContext asignado externamente) es la misma que ya usan
/// <c>LoginView</c>/<c>ShellView</c> de la Fase 4 -- se reutiliza el mismo patrón.
/// </para>
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Muestra, de forma modal, la ventana registrada para el tipo concreto de
    /// <paramref name="viewModel"/>, con <see cref="Window.Owner"/> igual a la ventana
    /// activa actual. Devuelve el <see cref="Window.DialogResult"/> de esa ventana
    /// (normalmente sin usar: los ViewModels de diálogo comunican su resultado con sus
    /// propias propiedades -- p.ej. <c>GuardadoExitoso</c> -- y un evento
    /// <c>SolicitarCierre</c> que el code-behind de la vista escucha para cerrar la
    /// ventana).
    /// </summary>
    bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class;

    /// <summary>Muestra un cuadro de diálogo de información simple (equivalente a <c>SwingUtils.mostrarInformacion</c>).</summary>
    void ShowInfo(string message, string title = "Información");

    /// <summary>Muestra un cuadro de diálogo de error simple (equivalente a <c>SwingUtils.mostrarError</c>).</summary>
    void ShowError(string message, string title = "Error");

    /// <summary>Pide confirmación Sí/No (equivalente a <c>SwingUtils.mostrarConfirmacion</c>).</summary>
    bool ShowConfirm(string message, string title = "Confirmar");

    /// <summary>Abre el selector de archivo estándar de Windows para elegir un archivo existente. Devuelve <c>null</c> si el usuario cancela.</summary>
    string? ShowOpenFileDialog(string filter, string title);

    /// <summary>Abre el selector de archivo estándar de Windows para elegir dónde guardar un archivo. Devuelve <c>null</c> si el usuario cancela.</summary>
    string? ShowSaveFileDialog(string filter, string title, string defaultFileName);

    /// <summary>
    /// Copia texto al portapapeles del sistema (Fase 6: botón "Copiar" del código de
    /// recuperación en el asistente de primer arranque y en la recuperación de
    /// contraseña). Se abstrae aquí -- en vez de que los ViewModels llamen directamente a
    /// <see cref="System.Windows.Clipboard"/> -- por el mismo motivo que el resto de esta
    /// interfaz: mantener los ViewModels testeables con Moq sin depender de un hilo STA
    /// real ni de infraestructura de WPF.
    /// </summary>
    void CopyToClipboard(string text);
}
