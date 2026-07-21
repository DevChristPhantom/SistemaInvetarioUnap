using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SisLabTopo.UI.Controls;

/// <summary>
/// Tarjeta de estadística reutilizable del dashboard (icono circular + título/valor/
/// descripción, y opcionalmente comparativa/tendencia + sparkline -- ver Fase B). Todas
/// sus propiedades son <see cref="DependencyProperty"/> bindeables para que el Dashboard
/// pueda actualizarlas dinámicamente (p.ej. tras refrescar datos) sin recrear el control.
/// </summary>
public partial class StatCard : UserControl
{
    /// <summary>Ancho, en píxeles, del área de dibujo del sparkline (ver <see cref="RecalcularSparklinePoints"/>).</summary>
    private const double SparklineAncho = 60;

    /// <summary>Alto, en píxeles, del área de dibujo del sparkline.</summary>
    private const double SparklineAlto = 24;

    /// <summary>Margen vertical interno para que la línea nunca toque el borde superior/inferior del área.</summary>
    private const double SparklinePadding = 2.5;

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

    /// <summary>
    /// Texto de comparativa/tendencia (p.ej. "+8% vs. mes anterior"). Vacío/null por
    /// defecto -- toda la fila 2 (tendencia + sparkline) permanece oculta mientras esta
    /// propiedad no se asigne, preservando exactamente el aspecto de la Fase 5 para
    /// cualquier StatCard existente que no la use.
    /// </summary>
    public static readonly DependencyProperty TrendTextProperty =
        DependencyProperty.Register(nameof(TrendText), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty, OnTrendTextChanged));

    /// <summary>
    /// "Up"/"Down"/"Flat": dirección de la tendencia, consumida por los estilos
    /// TrendArrowStyle/TrendTextStyle/SparklineStyle (StatCard.xaml) vía DataTrigger
    /// para pintar verde/rojo/gris con DynamicResource -- ver comentario XAML de por qué
    /// no se resuelve el color aquí en C# con un brush fijo.
    /// </summary>
    public static readonly DependencyProperty TrendDirectionProperty =
        DependencyProperty.Register(nameof(TrendDirection), typeof(string), typeof(StatCard), new PropertyMetadata("Flat"));

    private static readonly DependencyPropertyKey TrendVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TrendVisibility), typeof(Visibility), typeof(StatCard),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty TrendVisibilityProperty = TrendVisibilityPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey TrendArrowPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TrendArrow), typeof(string), typeof(StatCard), new PropertyMetadata("—"));

    public static readonly DependencyProperty TrendArrowProperty = TrendArrowPropertyKey.DependencyProperty;

    /// <summary>Serie de valores (orden cronológico, más antiguo primero) para el sparkline. Null/vacío/1 valor oculta el sparkline.</summary>
    public static readonly DependencyProperty SparklineValuesProperty =
        DependencyProperty.Register(nameof(SparklineValues), typeof(IReadOnlyList<double>), typeof(StatCard),
            new PropertyMetadata(null, OnSparklineValuesChanged));

    private static readonly DependencyPropertyKey SparklinePointsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SparklinePoints), typeof(PointCollection), typeof(StatCard),
            new PropertyMetadata(new PointCollection()));

    public static readonly DependencyProperty SparklinePointsProperty = SparklinePointsPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey SparklineVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SparklineVisibility), typeof(Visibility), typeof(StatCard),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty SparklineVisibilityProperty = SparklineVisibilityPropertyKey.DependencyProperty;

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

    /// <summary>Texto de comparativa/tendencia, p.ej. "+8% vs. mes anterior". Vacío = fila de tendencia oculta.</summary>
    public string TrendText
    {
        get => (string)GetValue(TrendTextProperty);
        set => SetValue(TrendTextProperty, value);
    }

    /// <summary>"Up", "Down" o "Flat" (por defecto).</summary>
    public string TrendDirection
    {
        get => (string)GetValue(TrendDirectionProperty);
        set => SetValue(TrendDirectionProperty, value);
    }

    public Visibility TrendVisibility => (Visibility)GetValue(TrendVisibilityProperty);

    public string TrendArrow => (string)GetValue(TrendArrowProperty);

    /// <summary>Serie de valores para el sparkline (orden cronológico ascendente).</summary>
    public IReadOnlyList<double>? SparklineValues
    {
        get => (IReadOnlyList<double>?)GetValue(SparklineValuesProperty);
        set => SetValue(SparklineValuesProperty, value);
    }

    public PointCollection SparklinePoints => (PointCollection)GetValue(SparklinePointsProperty);

    public Visibility SparklineVisibility => (Visibility)GetValue(SparklineVisibilityProperty);

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

    private static void OnTrendTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        var visible = !string.IsNullOrWhiteSpace((string?)e.NewValue);
        card.SetValue(TrendVisibilityPropertyKey, visible ? Visibility.Visible : Visibility.Collapsed);
        card.SetValue(TrendArrowPropertyKey, card.TrendDirection switch
        {
            "Up" => "▲",
            "Down" => "▼",
            _ => "—"
        });
    }

    private static void OnSparklineValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        card.RecalcularSparklinePoints((IReadOnlyList<double>?)e.NewValue);
    }

    /// <summary>
    /// Normaliza <paramref name="valores"/> (min-max) a un <see cref="PointCollection"/>
    /// dentro de un área fija de <see cref="SparklineAncho"/>x<see cref="SparklineAlto"/>
    /// píxeles. Con menos de 2 puntos el sparkline se oculta por completo (una sola
    /// muestra no comunica ninguna tendencia visual). Si todos los valores son iguales
    /// (rango 0), se dibuja una línea recta a media altura en vez de dividir por cero.
    /// </summary>
    private void RecalcularSparklinePoints(IReadOnlyList<double>? valores)
    {
        if (valores is null || valores.Count < 2)
        {
            SetValue(SparklineVisibilityPropertyKey, Visibility.Collapsed);
            SetValue(SparklinePointsPropertyKey, new PointCollection());
            return;
        }

        var min = valores.Min();
        var max = valores.Max();
        var rango = max - min;

        var puntos = new PointCollection(valores.Count);
        var alturaUtil = SparklineAlto - (SparklinePadding * 2);
        for (var i = 0; i < valores.Count; i++)
        {
            var x = valores.Count == 1 ? SparklineAncho / 2 : i / (double)(valores.Count - 1) * SparklineAncho;
            var proporcion = rango <= double.Epsilon ? 0.5 : (valores[i] - min) / rango;
            var y = SparklineAlto - SparklinePadding - (proporcion * alturaUtil);
            puntos.Add(new Point(x, y));
        }

        SetValue(SparklinePointsPropertyKey, puntos);
        SetValue(SparklineVisibilityPropertyKey, Visibility.Visible);
    }
}
