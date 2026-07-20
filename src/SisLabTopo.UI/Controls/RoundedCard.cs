using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SisLabTopo.UI.Controls;

/// <summary>
/// Tarjeta con esquinas redondeadas y fondo configurables. Puerto de
/// <c>ui.components.RoundedPanel</c> (Java, un <c>JPanel</c> con
/// <c>paintComponent</c> dibujando un <c>RoundRectangle2D</c>), pero implementado como
/// un <see cref="ContentControl"/> WPF real (con su plantilla en
/// <c>Themes/Controls.xaml</c>) en vez de override manual de pintura — así puede alojar
/// cualquier contenido XAML (no solo otro panel Swing) y participa normalmente en
/// layout/medición.
/// </summary>
public class RoundedCard : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(RoundedCard),
            new FrameworkPropertyMetadata(new CornerRadius(12)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    static RoundedCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RoundedCard), new FrameworkPropertyMetadata(typeof(RoundedCard)));
        BackgroundProperty.OverrideMetadata(typeof(RoundedCard), new FrameworkPropertyMetadata(Brushes.White));
    }
}
