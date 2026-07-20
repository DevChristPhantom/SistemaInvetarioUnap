using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SisLabTopo.UI.Controls;

/// <summary>
/// Tarjeta de estadística reutilizable del dashboard (icono circular + título/valor/
/// descripción). Todas sus propiedades son <see cref="DependencyProperty"/> bindeables
/// para que la Fase 5 pueda actualizarlas dinámicamente (p.ej. tras refrescar datos del
/// dashboard) sin recrear el control.
/// </summary>
public partial class StatCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty IconBrushProperty =
        DependencyProperty.Register(nameof(IconBrush), typeof(Brush), typeof(StatCard), new PropertyMetadata(Brushes.SteelBlue));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    private static readonly DependencyPropertyKey DescriptionVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DescriptionVisibility), typeof(Visibility), typeof(StatCard),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty DescriptionVisibilityProperty = DescriptionVisibilityPropertyKey.DependencyProperty;

    /// <summary>Título pequeño (gris) mostrado sobre el valor, p.ej. "Equipos Disponibles".</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Valor grande (22pt negrita) mostrado como cifra principal de la tarjeta.</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Texto pequeño opcional bajo el valor (p.ej. "de 42 registrados").</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Color de fondo del badge circular del icono.</summary>
    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    /// <summary>Glifo/texto corto mostrado dentro del badge circular (p.ej. un carácter Segoe MDL2/emoji-free).</summary>
    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public Visibility DescriptionVisibility => (Visibility)GetValue(DescriptionVisibilityProperty);

    public StatCard()
    {
        InitializeComponent();
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        var visible = !string.IsNullOrWhiteSpace((string?)e.NewValue);
        card.SetValue(DescriptionVisibilityPropertyKey, visible ? Visibility.Visible : Visibility.Collapsed);
    }
}
