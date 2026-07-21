using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SisLabTopo.UI.Controls;

/// <summary>
/// TextBox de búsqueda (con icono de lupa y botón de limpiar) sobre un
/// <see cref="DataGrid"/> estilizado, con filtrado case-insensitive y paginación en
/// memoria sobre la lista ya cargada. Puerto funcional de
/// <c>ui.components.SearchableTable&lt;T&gt;</c> (Java).
///
/// El filtro por defecto compara el texto de búsqueda contra el valor (vía
/// <c>ToString()</c>) de cada columna enlazada visible de <see cref="Columns"/>
/// (reflectando la ruta de <see cref="Binding.Path"/> de cada
/// <see cref="DataGridBoundColumn"/>), igual que <c>SearchableTable</c> filtraba sobre
/// las columnas visibles de su modelo. Si se necesita lógica de filtrado distinta, se
/// puede inyectar vía <see cref="FilterPredicate"/>.
///
/// Fase B (rediseño de tablas): paginación añadida en memoria sobre <see cref="_filtrados"/>
/// (la lista ya filtrada, materializada de <see cref="ItemsSource"/>) -- NO se agrega
/// ningún método nuevo a SisLabTopo.Services ni se toca ningún ViewModel de pantalla:
/// al vivir en este control compartido, Equipos/Préstamos/Historial/Disponibilidad
/// Rápida (Dashboard) la reciben automáticamente. Decisión de diseño explícita: el
/// ordenamiento por columna (click en header) se deshabilita
/// (<c>CanUserSortColumns="False"</c> en el XAML) porque el DataGrid solo ve la
/// "ventana" de la página actual -- permitir su propio sort ahí solo reordenaría esos
/// N elementos visibles, no el conjunto filtrado completo, lo cual sería confuso e
/// incorrecto. Ninguna pantalla/prueba existente dependía de esa función (no había
/// ningún <c>CanUserSortColumns</c>/<c>SortMemberPath</c> en todo el proyecto antes de
/// este cambio).
/// </summary>
public partial class SearchableDataGrid : UserControl
{
    private const int PageSizePorDefecto = 12;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(SearchableDataGrid),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(SearchableDataGrid),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(SearchableDataGrid),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

    private static readonly DependencyPropertyKey HasSearchTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasSearchText), typeof(bool), typeof(SearchableDataGrid),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HasSearchTextProperty = HasSearchTextPropertyKey.DependencyProperty;

    public static readonly DependencyProperty FilterPredicateProperty =
        DependencyProperty.Register(nameof(FilterPredicate), typeof(Func<object, string, bool>), typeof(SearchableDataGrid),
            new PropertyMetadata(null));

    /// <summary>Elementos por página. 12 por defecto (visible sin recortar tarjetas/mini-tablas pequeñas de Dashboard); las pantallas grandes pueden subirlo.</summary>
    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(SearchableDataGrid),
            new PropertyMetadata(PageSizePorDefecto, OnPageSizeChanged));

    private static readonly DependencyPropertyKey CurrentPagePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CurrentPage), typeof(int), typeof(SearchableDataGrid),
            new PropertyMetadata(1));

    public static readonly DependencyProperty CurrentPageProperty = CurrentPagePropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey TotalPagesPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(TotalPages), typeof(int), typeof(SearchableDataGrid),
            new PropertyMetadata(1));

    public static readonly DependencyProperty TotalPagesProperty = TotalPagesPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ShowPaginationPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ShowPagination), typeof(bool), typeof(SearchableDataGrid),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowPaginationProperty = ShowPaginationPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey CanGoPreviousPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CanGoPrevious), typeof(bool), typeof(SearchableDataGrid),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanGoPreviousProperty = CanGoPreviousPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey CanGoNextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CanGoNext), typeof(bool), typeof(SearchableDataGrid),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanGoNextProperty = CanGoNextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey PageInfoTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(PageInfoText), typeof(string), typeof(SearchableDataGrid),
            new PropertyMetadata("Sin resultados"));

    public static readonly DependencyProperty PageInfoTextProperty = PageInfoTextPropertyKey.DependencyProperty;

    private bool _suppressSelectionCallback;
    private List<object> _filtrados = new();

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Fila seleccionada, bindeable en dos sentidos (equivalente a <c>getSelectedRow()</c> de <c>SearchableTable</c>).</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool HasSearchText => (bool)GetValue(HasSearchTextProperty);

    /// <summary>
    /// Función de filtro inyectable opcional: <c>(item, textoBusqueda) =&gt; coincide</c>.
    /// Si es <c>null</c>, se usa el filtro por defecto basado en las columnas visibles.
    /// </summary>
    public Func<object, string, bool>? FilterPredicate
    {
        get => (Func<object, string, bool>?)GetValue(FilterPredicateProperty);
        set => SetValue(FilterPredicateProperty, value);
    }

    /// <summary>Cantidad de filas mostradas por página.</summary>
    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>Página actual (1-based) dentro del conjunto ya filtrado.</summary>
    public int CurrentPage => (int)GetValue(CurrentPageProperty);

    /// <summary>Total de páginas del conjunto ya filtrado (mínimo 1).</summary>
    public int TotalPages => (int)GetValue(TotalPagesProperty);

    /// <summary>Verdadero solo cuando el conjunto filtrado excede una página -- oculta la barra de paginación si todo cabe en una sola página.</summary>
    public bool ShowPagination => (bool)GetValue(ShowPaginationProperty);

    public bool CanGoPrevious => (bool)GetValue(CanGoPreviousProperty);

    public bool CanGoNext => (bool)GetValue(CanGoNextProperty);

    /// <summary>Texto "Página X de Y · N resultado(s)" mostrado junto a los botones Anterior/Siguiente.</summary>
    public string PageInfoText => (string)GetValue(PageInfoTextProperty);

    /// <summary>Columnas del DataGrid interno; declarar en XAML vía &lt;controls:SearchableDataGrid.Columns&gt;.</summary>
    public ObservableCollection<DataGridColumn> Columns => PART_DataGrid.Columns;

    /// <summary>Acceso directo al DataGrid interno para casos avanzados (estilos de fila condicionales, etc.).</summary>
    public DataGrid InnerDataGrid => PART_DataGrid;

    public SearchableDataGrid()
    {
        InitializeComponent();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SearchableDataGrid)d;
        control.AplicarFiltroYPagina(reiniciarPagina: true);
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SearchableDataGrid)d;
        if (control._suppressSelectionCallback)
        {
            return;
        }

        // Si el ítem seleccionado (asignado desde el ViewModel) no está en la página
        // visible actual, WPF simplemente no resalta ninguna fila (no lanza excepción)
        // -- preferible a saltar de página sin que el usuario lo haya pedido.
        control.PART_DataGrid.SelectedItem = e.NewValue;
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SearchableDataGrid)d;
        control.SetValue(HasSearchTextPropertyKey, !string.IsNullOrEmpty((string?)e.NewValue));
        control.AplicarFiltroYPagina(reiniciarPagina: true);
    }

    private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SearchableDataGrid)d;
        control.AplicarFiltroYPagina(reiniciarPagina: true);
    }

    private void InnerDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _suppressSelectionCallback = true;
        try
        {
            SelectedItem = PART_DataGrid.SelectedItem;
        }
        finally
        {
            _suppressSelectionCallback = false;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) => SearchText = string.Empty;

    private void PreviousPageButton_Click(object sender, RoutedEventArgs e) => IrAPagina(CurrentPage - 1);

    private void NextPageButton_Click(object sender, RoutedEventArgs e) => IrAPagina(CurrentPage + 1);

    private void IrAPagina(int pagina)
    {
        SetValue(CurrentPagePropertyKey, pagina);
        AplicarFiltroYPagina(reiniciarPagina: false);
    }

    /// <summary>
    /// Recalcula el conjunto filtrado a partir de <see cref="ItemsSource"/> +
    /// <see cref="SearchText"/>, y luego recorta la "ventana" de la página actual que
    /// se asigna como <c>ItemsSource</c> real del DataGrid interno. Se llama tras
    /// cualquier cambio de origen de datos, texto de búsqueda, tamaño de página o
    /// navegación de página.
    /// </summary>
    private void AplicarFiltroYPagina(bool reiniciarPagina)
    {
        IEnumerable<object> origen = ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var texto = SearchText.Trim();
            origen = origen.Where(item => Coincide(item, texto));
        }

        _filtrados = origen.ToList();

        var pageSize = Math.Max(1, PageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(_filtrados.Count / (double)pageSize));
        var paginaActual = reiniciarPagina ? 1 : Math.Clamp(CurrentPage, 1, totalPages);

        SetValue(TotalPagesPropertyKey, totalPages);
        SetValue(CurrentPagePropertyKey, paginaActual);
        SetValue(ShowPaginationPropertyKey, _filtrados.Count > pageSize);
        SetValue(CanGoPreviousPropertyKey, paginaActual > 1);
        SetValue(CanGoNextPropertyKey, paginaActual < totalPages);
        SetValue(PageInfoTextPropertyKey, _filtrados.Count == 0
            ? "Sin resultados"
            : $"Página {paginaActual} de {totalPages}  ·  {_filtrados.Count} resultado(s)");

        var elementosPagina = _filtrados.Skip((paginaActual - 1) * pageSize).Take(pageSize).ToList();
        PART_DataGrid.ItemsSource = elementosPagina;
        PART_DataGrid.SelectedItem = SelectedItem is not null && elementosPagina.Contains(SelectedItem) ? SelectedItem : null;
    }

    private bool Coincide(object item, string texto)
    {
        if (FilterPredicate is { } predicate)
        {
            return predicate(item, texto);
        }

        foreach (var column in PART_DataGrid.Columns)
        {
            if (column.Visibility != Visibility.Visible || column is not DataGridBoundColumn bound)
            {
                continue;
            }

            if (bound.Binding is not Binding binding || string.IsNullOrEmpty(binding.Path?.Path))
            {
                continue;
            }

            var valor = ObtenerValorPorRuta(item, binding.Path.Path);
            if (valor is not null && valor.Contains(texto, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ObtenerValorPorRuta(object item, string path)
    {
        object? current = item;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            current = property?.GetValue(current);
        }

        return current?.ToString();
    }
}
