namespace SisLabTopo.UI.Theming;

/// <summary>Los dos temas visuales soportados por el rediseño (ver <see cref="IThemeService"/>).</summary>
public enum AppTheme
{
    Claro,
    Oscuro,
}

/// <summary>
/// Alterna en caliente entre <c>Themes/Colors.xaml</c> (claro) y
/// <c>Themes/Colors.Dark.xaml</c> (oscuro) reemplazando el <see cref="System.Windows.ResourceDictionary"/>
/// de colores dentro de <c>Application.Current.Resources.MergedDictionaries</c>. Ambos
/// diccionarios declaran exactamente las mismas claves (<c>PrimaryBrush</c>,
/// <c>CardBrush</c>, <c>TextDarkBrush</c>, etc.), así que toda vista que ya consume esos
/// recursos vía <c>StaticResource</c>... en realidad NO se actualiza sola con
/// StaticResource puro (StaticResource resuelve una sola vez); por eso el intercambio
/// reemplaza el ResourceDictionary COMPLETO (no los recursos uno a uno) y fuerza,
/// además, una invalidación de todo el árbol visual para que WPF vuelva a evaluar los
/// StaticResource ya resueltos -- ver <see cref="ThemeService.RefrescarArbolVisual"/>.
/// </summary>
public interface IThemeService
{
    AppTheme TemaActual { get; }

    /// <summary>Aplica el tema indicado a toda la aplicación (idempotente si ya es el tema actual).</summary>
    void AplicarTema(AppTheme tema);

    /// <summary>Alterna entre Claro y Oscuro.</summary>
    void AlternarTema();

    /// <summary>Se dispara después de aplicar un tema, para que las vistas puedan reaccionar si lo necesitan.</summary>
    event EventHandler<AppTheme>? TemaCambiado;
}
