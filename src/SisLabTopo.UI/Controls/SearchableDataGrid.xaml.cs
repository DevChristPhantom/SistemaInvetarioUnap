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
/// <see cref="DataGrid"/> estilizado, con filtrado case-insensitive vía
/// <see cref="ICollectionView.Filter"/>. Puerto funcional de
/// <c>ui.components.SearchableTable&lt;T&gt;</c> (Java).
///
/// El filtro por defecto compara el texto de búsqueda contra el valor (vía
/// <c>ToString()</c>) de cada columna enlazada visible de <see cref="Columns"/>
/// (reflectando la ruta de <see cref="Binding.Path"/> de cada
/// <see cref="DataGridBoundColumn"/>), igual que <c>SearchableTable</c> filtraba sobre
/// las columnas visibles de su modelo. Si se necesita lógica de filtrado distinta, se
/// puede inyectar vía <see cref="FilterPredicate"/>.
/// </summary>
public partial class SearchableDataGrid : UserControl
{
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

    private bool _suppressSelectionCallback;

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
        control.PART_DataGrid.ItemsSource = e.NewValue as IEnumerable;
        control.RefreshFilter();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SearchableDataGrid)d;
        if (control._suppressSelectionCallback)
        {
            return;
        }

        control.PART_DataGrid.SelectedItem = e.NewValue;
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SearchableDataGrid)d;
        control.SetValue(HasSearchTextPropertyKey, !string.IsNullOrEmpty((string?)e.NewValue));
        control.RefreshFilter();
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

    private void RefreshFilter()
    {
        var view = CollectionViewSource.GetDefaultView(PART_DataGrid.ItemsSource);
        if (view is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            view.Filter = null;
            return;
        }

        var texto = SearchText.Trim();
        view.Filter = item => Coincide(item, texto);
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
