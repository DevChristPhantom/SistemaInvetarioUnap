using System.Windows;

namespace SisLabTopo.UI.Equipos;

/// <summary>
/// Vista del formulario de alta/edición de equipo. Única responsabilidad de
/// code-behind: cerrarse cuando el ViewModel dispara <c>SolicitarCierre</c> (al guardar
/// con éxito o al cancelar) -- el <c>DataContext</c> lo asigna
/// <see cref="Dialogs.DialogService"/>.
/// </summary>
public partial class EquipoFormView : Window
{
    public EquipoFormView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is EquipoFormViewModel anterior)
            {
                anterior.SolicitarCierre -= OnSolicitarCierre;
            }

            if (e.NewValue is EquipoFormViewModel nuevo)
            {
                nuevo.SolicitarCierre += OnSolicitarCierre;
            }
        };
    }

    private void OnSolicitarCierre(object? sender, EventArgs e)
    {
        // Asignar DialogResult (solo válido tras ShowDialog, que es como
        // DialogService siempre abre esta ventana) cierra la ventana automáticamente.
        DialogResult = sender is EquipoFormViewModel { GuardadoExitoso: true };
    }
}
