using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using UglyToad.PdfPig;
using static SisLabTopo.Reports.Tests.ComprobanteTestFixtures;

namespace SisLabTopo.Reports.Tests;

/// <summary>
/// Verifica que <see cref="ComprobantePrestamoGenerator"/> reproduce el comprobante de
/// préstamo tal como lo generaba <c>PDFBoxComprobantePrinter</c> (Java): tamaño de página,
/// una sola página, textos esperados en cada sección, la tabla fija de 7 filas (llena y casi
/// vacía), truncado con elipsis de nombres largos y sanitización de caracteres tipográficos.
///
/// No hay comparación visual automática (eso queda para revisión manual — ver
/// <see cref="SamplePdfGenerationTests"/> para los PDFs de muestra); estas pruebas verifican
/// contenido y estructura extrayendo texto con PdfPig.
/// </summary>
public class ComprobantePrestamoGeneratorTests
{
    private readonly ComprobantePrestamoGenerator _generator = new();

    private static string ExtraerTexto(string rutaPdf)
    {
        using var doc = PdfDocument.Open(rutaPdf);
        return doc.GetPage(1).Text;
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaProducirUnaPaginaA4()
    {
        var prestamo = CrearPrestamo();
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "EST-001") };
        var equipos = CatalogoEquipos();

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            using var doc = PdfDocument.Open(ruta);
            Assert.Equal(1, doc.NumberOfPages);
            var page = doc.GetPage(1);
            // A4 = 595 x 842 puntos.
            Assert.Equal(595, page.Width, 0.5);
            Assert.Equal(842, page.Height, 0.5);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaContenerLosDatosDelSolicitanteYElEncabezadoInstitucional()
    {
        var prestamo = CrearPrestamo(
            docente: "Ing. Rosa Mamani Flores",
            curso: "Geodesia",
            semestre: "2026-II",
            nombreEstudiante: "Carlos Alberto Quispe Turpo",
            codigoEstudiante: "202099887");
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "EST-001") };
        var equipos = CatalogoEquipos();

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            var texto = ExtraerTexto(ruta);

            Assert.Contains("Universidad Nacional del Altiplano", texto);
            Assert.Contains("FACULTAD DE INGENIERÍA DE MINAS", texto);
            Assert.Contains("ESCUELA ACREDITADA POR ICACIT", texto);
            Assert.Contains("Año de la Esperanza y el Fortalecimiento de la Democracia", texto);

            Assert.Contains("Docente Responsable:", texto);
            Assert.Contains("Ing. Rosa Mamani Flores", texto);
            Assert.Contains("Geodesia", texto);
            Assert.Contains("2026-II", texto);
            Assert.Contains("202099887", texto);
            Assert.Contains("Nombre del Estudiante:", texto);
            Assert.Contains("Carlos Alberto Quispe Turpo", texto);

            Assert.Contains("Por el presente, hago constar que recibí los siguientes equipos", texto);
            Assert.Contains("Laboratorio de Topografía Minera de la FIM", texto);

            Assert.Contains("N°", texto);
            Assert.Contains("Descripción del Equipo", texto);
            Assert.Contains("Código Patrimonial", texto);

            Assert.Contains("FIRMA DEL DOCENTE", texto);
            Assert.Contains("FIRMA DEL ESTUDIANTE", texto);
            Assert.Contains("Observaciones:", texto);

            // La fecha del comprobante es SIEMPRE la de generación (hoy), nunca prestamo.FechaPrestamo.
            var fechaEsperada = DateTime.Today.ToString("dd 'de' MMMM 'del' yyyy", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
            Assert.Contains($"Puno, C.U., {fechaEsperada}", texto);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaListarLosSieteEquipos_CuandoLaTablaEstaCompleta()
    {
        var prestamo = CrearPrestamo();
        var equipos = CatalogoEquipos();
        var detalles = equipos.Select(e => CrearDetalle(prestamo.Id, e.Codigo)).ToList();
        Assert.Equal(7, detalles.Count);

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            var texto = ExtraerTexto(ruta);
            foreach (var equipo in equipos)
            {
                Assert.Contains(equipo.Denominacion, texto);
                Assert.Contains(equipo.Codigo, texto);
            }

            // Los números de fila 1..7 deben estar presentes (tabla fija de 7 filas).
            for (var i = 1; i <= 7; i++)
            {
                Assert.Contains(i.ToString(), texto);
            }
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaGenerarTablaCasiVacia_CuandoHayUnSoloDetalle()
    {
        var prestamo = CrearPrestamo();
        var equipos = CatalogoEquipos();
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "EST-001") };

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            using var doc = PdfDocument.Open(ruta);
            Assert.Equal(1, doc.NumberOfPages); // FILAS_TABLA=7 fijas: nunca crece a una 2da página.
            var texto = doc.GetPage(1).Text;
            Assert.Contains("Estación Total Topcon GPT-3000", texto);
            Assert.Contains("EST-001", texto);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaUsarDenominacionEquipo_ConFallback_CuandoElCodigoNoExisteEnElCatalogo()
    {
        var prestamo = CrearPrestamo();
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "CODIGO-INEXISTENTE") };
        var equipos = CatalogoEquipos();

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            var texto = ExtraerTexto(ruta);
            Assert.Contains("Equipo", texto); // fallback literal, igual que Java
            Assert.Contains("CODIGO-INEXISTENTE", texto);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaTruncarConElipsis_CuandoElNombreDelEquipoEsMuyLargo()
    {
        var prestamo = CrearPrestamo();
        const string nombreLargo = "Trípode de aluminio para nivel automático de alta precisión con extensión telescópica reforzada y estuche protector resistente al agua";
        var equipos = new List<Equipo> { CrearEquipo("TRIP-999", nombreLargo) };
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "TRIP-999") };

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            var texto = ExtraerTexto(ruta);
            Assert.DoesNotContain(nombreLargo, texto); // el texto completo NO debe caber sin truncar
            Assert.Contains("...", texto); // se añadió la elipsis ASCII de 3 puntos
            Assert.Contains("TRIP-999", texto); // el código nunca se trunca
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaSanitizarCaracteresTipograficos_EnObservaciones()
    {
        var prestamo = CrearPrestamo(observaciones: "Entregado en buen estado – revisar la ‘calibración’ y el “enfoque” … pendiente");
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "EST-001") };
        var equipos = CatalogoEquipos();

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            var texto = ExtraerTexto(ruta);

            // Los caracteres tipográficos originales no deben aparecer en el PDF.
            Assert.DoesNotContain("–", texto);
            Assert.DoesNotContain("‘", texto);
            Assert.DoesNotContain("’", texto);
            Assert.DoesNotContain("“", texto);
            Assert.DoesNotContain("”", texto);
            Assert.DoesNotContain("…", texto);

            // Deben aparecer sus equivalentes rectos/ASCII.
            Assert.Contains("revisar la 'calibración' y el \"enfoque\" . pendiente", texto);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaLanzarArgumentNullException_CuandoElPrestamoEsNulo()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _generator.GenerarPdfAsync(null!, [], []));
    }

    [Fact]
    public async Task GenerarPdfAsync_DeberiaManejarObservacionesYCamposNulos_SinLanzarExcepcion()
    {
        var prestamo = CrearPrestamo(observaciones: null);
        var detalles = new List<DetallePrestamo> { new() { Id = "1", PrestamoId = prestamo.Id, EquipoCodigo = null! } };
        var equipos = CatalogoEquipos();

        var ruta = await _generator.GenerarPdfAsync(prestamo, detalles, equipos);

        try
        {
            Assert.True(File.Exists(ruta));
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void PrintException_DeberiaEnvolverErroresDeGeneracion()
    {
        // Prueba de contrato: verifica que el tipo de excepción usado para errores de
        // generación/impresión existe y expone los constructores esperados (mensaje,
        // mensaje + inner exception), igual que exception.PrintException en Java.
        var inner = new IOException("fallo simulado");
        var ex = new PrintException("Error al escribir el comprobante en formato PDF.", inner);

        Assert.Equal("Error al escribir el comprobante en formato PDF.", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
