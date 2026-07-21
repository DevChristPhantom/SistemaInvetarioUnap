using System.Windows;

namespace SisLabTopo.UI.Theming;

/// <summary>
/// Implementación de <see cref="IThemeService"/>.
///
/// <para>
/// <b>Por qué NO simplemente "reemplazar el ResourceDictionary de Colors.xaml en
/// MergedDictionaries"</b> (que fue el primer diseño de esta clase, descartado tras
/// verificación visual real -- ver más abajo): <c>Themes/Controls.xaml</c> declara su
/// propio <c>&lt;ResourceDictionary.MergedDictionaries&gt;</c> que vuelve a fusionar
/// <c>Colors.xaml</c> (para poder resolver sus propios <c>StaticResource</c>/
/// <c>DynamicResource</c> internos si se carga de forma independiente). Como
/// <c>App.xaml</c> fusiona <c>Colors.xaml</c> y <c>Controls.xaml</c> como HERMANOS en
/// <c>Application.Resources.MergedDictionaries</c> (Colors primero, Controls al
/// final), la copia de <c>Colors.xaml</c> anidada DENTRO de <c>Controls.xaml</c> queda,
/// en la práctica, en una posición de búsqueda de recursos con más precedencia que la
/// copia "suelta" de nivel superior -- así que reemplazar solo esa copia suelta
/// (índice 0) NUNCA cambiaba lo que la mayoría de estilos veían realmente, porque la
/// copia clara embebida en Controls.xaml seguía "ganando". Este bug se detectó al
/// capturar una screenshot real de <see cref="Shell.ShellView"/> antes/después de
/// alternar el tema: la segunda captura era pixel-idéntica a la primera.
/// </para>
/// <para>
/// <b>Solución adoptada</b>: en vez de tocar <c>MergedDictionaries</c>, se copian las
/// claves del diccionario del tema elegido DIRECTAMENTE sobre
/// <c>Application.Current.Resources</c> (el diccionario propio de la Application, no
/// uno de sus <c>MergedDictionaries</c>). En WPF, las claves PROPIAS de un
/// ResourceDictionary siempre tienen precedencia sobre las de CUALQUIERA de sus
/// MergedDictionaries (a cualquier profundidad de anidamiento), así que esta técnica
/// gana sí o sí sin importar cuántas copias anidadas de Colors.xaml existan en otros
/// diccionarios (Controls.xaml, o cualquier otro que se añada en el futuro). Como
/// además cada clave se asigna con un valor CONCRETO (no un <c>ResourceDictionary</c>
/// nuevo), cualquier control que consuma esas claves con <c>DynamicResource</c> se
/// entera del cambio de inmediato (WPF dispara la invalidación de recursos al
/// modificar <c>Resources[clave]</c>), sin necesidad de recrear ninguna ventana.
/// </para>
/// </summary>
public sealed class ThemeService : IThemeService
{
    public AppTheme TemaActual { get; private set; } = AppTheme.Claro;

    public event EventHandler<AppTheme>? TemaCambiado;

    public void AplicarTema(AppTheme tema)
    {
        var app = Application.Current;
        if (app is null)
        {
            // Contexto sin Application real (p.ej. algunas pruebas unitarias que no
            // levantan WPF de verdad): no hay Resources que tocar.
            TemaActual = tema;
            return;
        }

        var uriRelativo = tema == AppTheme.Oscuro ? "Themes/Colors.Dark.xaml" : "Themes/Colors.xaml";
        var origen = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/SisLabTopo.UI;component/{uriRelativo}", UriKind.Absolute),
        };

        foreach (var clave in origen.Keys)
        {
            app.Resources[clave] = origen[clave];
        }

        TemaActual = tema;
        TemaCambiado?.Invoke(this, tema);
    }

    public void AlternarTema() =>
        AplicarTema(TemaActual == AppTheme.Claro ? AppTheme.Oscuro : AppTheme.Claro);
}
