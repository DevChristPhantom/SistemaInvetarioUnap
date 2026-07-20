using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SisLabTopo.UI.Controls;

/// <summary>
/// Puerto del patrón "píldora de estado" que en Java se repetía dibujado a mano
/// (<c>RoundedPanel</c> + <c>JLabel</c>) en cada pantalla que mostraba un estado. Aquí es
/// un único control con dos propiedades bindeables: <see cref="BadgeText"/> y
/// <see cref="BadgeBrush"/>. Para bindear directamente un <c>EstadoEquipo</c>,
/// <c>EstadoPrestamo</c> o un <c>bool</c> de disponibilidad, usar los converters de
/// <c>SisLabTopo.UI.Converters</c> (<c>EstadoEquipoToTextConverter</c> +
/// <c>EstadoEquipoToBrushConverter</c>, etc.) sobre estas dos propiedades.
/// </summary>
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(nameof(BadgeText), typeof(string), typeof(StatusBadge),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeBrushProperty =
        DependencyProperty.Register(nameof(BadgeBrush), typeof(Brush), typeof(StatusBadge),
            new PropertyMetadata(Brushes.Gray));

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public Brush BadgeBrush
    {
        get => (Brush)GetValue(BadgeBrushProperty);
        set => SetValue(BadgeBrushProperty, value);
    }

    public StatusBadge()
    {
        InitializeComponent();
    }
}
