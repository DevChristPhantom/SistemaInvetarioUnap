using System.Reflection;
using System.Windows;
using SisLabTopo.UI.Controls;

namespace SisLabTopo.UI.Tests;

/// <summary>
/// Pruebas de la paginación en memoria agregada a <see cref="SearchableDataGrid"/> en la
/// Fase B (rediseño de tablas): construyen el control de verdad (no un doble/mock) en un
/// hilo STA -- ver <see cref="LoginViewRenderingTests"/>, cuyo arnés de
/// Application/MergedDictionaries se reutiliza aquí porque <c>SearchableDataGrid.xaml</c>
/// referencia <c>StaticResource</c>s de los mismos diccionarios de tema (TextBoxCornerRadius,
/// LinkButtonStyle, SmallStyle, SecondaryButtonStyle, BoolToVisibilityConverter) -- y
/// SIN necesitar mostrar ninguna ventana en pantalla, a diferencia de esa otra prueba.
/// </summary>
public class SearchableDataGridPaginationTests
{
    private sealed class FilaDePrueba
    {
        public int Numero { get; init; }
    }

    private static SearchableDataGrid CrearGrid(int pageSize, int totalElementos)
    {
        LoginViewRenderingTests.AsegurarRecursosDeAplicacion();

        var grid = new SearchableDataGrid { PageSize = pageSize };
        grid.ItemsSource = Enumerable.Range(1, totalElementos)
            .Select(numero => new FilaDePrueba { Numero = numero })
            .ToList();
        return grid;
    }

    /// <summary>Invoca los manejadores <c>Click</c> privados de los botones Anterior/Siguiente -- no expuestos como API pública del control, solo como respuesta a un click real de usuario.</summary>
    private static void Click(SearchableDataGrid grid, string nombreManejador)
    {
        var metodo = typeof(SearchableDataGrid).GetMethod(nombreManejador, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(SearchableDataGrid), nombreManejador);
        metodo.Invoke(grid, new object?[] { null, new RoutedEventArgs() });
    }

    [Fact]
    public void ItemsSource_ConMasFilasQuePageSize_MuestraSoloLaPrimeraPaginaYHabilitaSiguiente()
    {
        var resultado = LoginViewRenderingTests.EjecutarEnHiloSta(() =>
        {
            var grid = CrearGrid(pageSize: 10, totalElementos: 25);
            var primeraPagina = grid.InnerDataGrid.ItemsSource!.Cast<FilaDePrueba>().ToList();

            return (
                TotalPages: grid.TotalPages, CurrentPage: grid.CurrentPage, ShowPagination: grid.ShowPagination,
                CanGoNext: grid.CanGoNext, CanGoPrevious: grid.CanGoPrevious, PageInfoText: grid.PageInfoText,
                CantidadEnPagina: primeraPagina.Count, PrimerNumero: primeraPagina[0].Numero, UltimoNumero: primeraPagina[^1].Numero);
        });

        Assert.Equal(3, resultado.TotalPages);
        Assert.Equal(1, resultado.CurrentPage);
        Assert.True(resultado.ShowPagination);
        Assert.True(resultado.CanGoNext);
        Assert.False(resultado.CanGoPrevious);
        Assert.Equal("Página 1 de 3  ·  25 resultado(s)", resultado.PageInfoText);
        Assert.Equal(10, resultado.CantidadEnPagina);
        Assert.Equal(1, resultado.PrimerNumero);
        Assert.Equal(10, resultado.UltimoNumero);
    }

    [Fact]
    public void ItemsSource_ConMenosFilasQuePageSize_OcultaLaBarraDePaginacion()
    {
        var (showPagination, canNext, totalPages) = LoginViewRenderingTests.EjecutarEnHiloSta(() =>
        {
            var grid = CrearGrid(pageSize: 10, totalElementos: 4);
            return (grid.ShowPagination, grid.CanGoNext, grid.TotalPages);
        });

        Assert.False(showPagination);
        Assert.False(canNext);
        Assert.Equal(1, totalPages);
    }

    [Fact]
    public void Siguiente_AvanzaLaVentanaVisibleYHabilitaAnterior()
    {
        var resultado = LoginViewRenderingTests.EjecutarEnHiloSta(() =>
        {
            var grid = CrearGrid(pageSize: 10, totalElementos: 25);

            Click(grid, "NextPageButton_Click");
            var segundaPagina = grid.InnerDataGrid.ItemsSource!.Cast<FilaDePrueba>().ToList();

            return (grid.CurrentPage, grid.CanGoNext, grid.CanGoPrevious, segundaPagina.Count, segundaPagina[0].Numero);
        });

        Assert.Equal(2, resultado.CurrentPage);
        Assert.True(resultado.CanGoNext);
        Assert.True(resultado.CanGoPrevious);
        Assert.Equal(10, resultado.Count);
        Assert.Equal(11, resultado.Numero);
    }

    [Fact]
    public void Siguiente_EnLaUltimaPagina_MuestraElResiduoYDeshabilitaSiguiente()
    {
        var resultado = LoginViewRenderingTests.EjecutarEnHiloSta(() =>
        {
            var grid = CrearGrid(pageSize: 10, totalElementos: 25);

            Click(grid, "NextPageButton_Click");
            Click(grid, "NextPageButton_Click");
            var terceraPagina = grid.InnerDataGrid.ItemsSource!.Cast<FilaDePrueba>().ToList();

            return (grid.CurrentPage, grid.CanGoNext, terceraPagina.Count, terceraPagina[0].Numero);
        });

        Assert.Equal(3, resultado.CurrentPage);
        Assert.False(resultado.CanGoNext);
        // 25 filas / PageSize 10 -> última página con el residuo (5), no 10 completas.
        Assert.Equal(5, resultado.Count);
        Assert.Equal(21, resultado.Numero);
    }

    [Fact]
    public void SiguienteLuegoAnterior_RegresaExactamenteALaPrimeraPagina()
    {
        var resultado = LoginViewRenderingTests.EjecutarEnHiloSta(() =>
        {
            var grid = CrearGrid(pageSize: 10, totalElementos: 25);

            Click(grid, "NextPageButton_Click");
            Click(grid, "PreviousPageButton_Click");
            var primeraPagina = grid.InnerDataGrid.ItemsSource!.Cast<FilaDePrueba>().ToList();

            return (grid.CurrentPage, grid.CanGoPrevious, primeraPagina[0].Numero);
        });

        Assert.Equal(1, resultado.CurrentPage);
        Assert.False(resultado.CanGoPrevious);
        Assert.Equal(1, resultado.Numero);
    }

    [Fact]
    public void SearchText_FiltraAntesDePaginarYReiniciaALaPrimeraPagina()
    {
        var resultado = LoginViewRenderingTests.EjecutarEnHiloSta(() =>
        {
            // FilaDePrueba no tiene columnas bindeadas (el control no las necesita para
            // paginar), así que se inyecta un FilterPredicate explícito -- el filtro por
            // defecto basado en columnas visibles no aplica sin columnas declaradas.
            var grid = CrearGrid(pageSize: 10, totalElementos: 25);
            grid.FilterPredicate = (item, texto) => ((FilaDePrueba)item).Numero.ToString().Contains(texto);

            Click(grid, "NextPageButton_Click"); // a la página 2, para verificar que el filtro reinicia a la página 1
            grid.SearchText = "1"; // coincide con 1,10-19,21

            return (grid.CurrentPage, grid.TotalPages, grid.PageInfoText);
        });

        Assert.Equal(1, resultado.CurrentPage);
        // Coincidencias de "1": 1, 10,11,12,13,14,15,16,17,18,19, 21 -> 12 resultados -> 2 páginas de a 10.
        Assert.Equal(2, resultado.TotalPages);
        Assert.Equal("Página 1 de 2  ·  12 resultado(s)", resultado.PageInfoText);
    }
}
