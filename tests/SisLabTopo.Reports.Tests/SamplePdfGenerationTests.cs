using SisLabTopo.Domain.Models;
using static SisLabTopo.Reports.Tests.ComprobanteTestFixtures;

namespace SisLabTopo.Reports.Tests;

/// <summary>
/// No verifica nada por sí sola (no hay comparación visual automática posible) — genera un
/// puñado de PDFs de muestra representativos en un directorio conocido de salida para que el
/// usuario pueda abrirlos y comparar a ojo contra el comprobante que producía la versión Java
/// (<c>PDFBoxComprobantePrinter</c>) antes de dar la Fase 3 por cerrada visualmente.
///
/// Se ejecuta como parte de la suite normal de <c>dotnet test</c> (no requiere ningún paso
/// manual adicional); los archivos quedan en <c>SamplePdfs/</c> junto al ensamblado de
/// pruebas compilado.
/// </summary>
public class SamplePdfGenerationTests
{
    private static string DirectorioSalida()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "SamplePdfs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task GenerarMuestra_TablaCompleta_SieteItems()
    {
        var generator = new ComprobantePrestamoGenerator();
        var prestamo = CrearPrestamo(
            docente: "Ing. Juan Pérez Quispe",
            curso: "Topografía II",
            semestre: "2026-I",
            nombreEstudiante: "María Fernanda Huanca Condori",
            codigoEstudiante: "201812345",
            observaciones: "Equipos entregados en buen estado. Devolución programada al finalizar la práctica.");
        var equipos = CatalogoEquipos();
        var detalles = equipos.Select(e => CrearDetalle(prestamo.Id, e.Codigo)).ToList();

        var ruta = await generator.GenerarPdfAsync(prestamo, detalles, equipos);
        var destino = Path.Combine(DirectorioSalida(), "muestra_01_tabla_completa_7items.pdf");
        File.Copy(ruta, destino, overwrite: true);

        Assert.True(File.Exists(destino));
    }

    [Fact]
    public async Task GenerarMuestra_UnSoloItem_ConNombreLargoTruncado()
    {
        var generator = new ComprobantePrestamoGenerator();
        var prestamo = CrearPrestamo(
            docente: "Ing. Rosa Mamani Flores",
            curso: "Geodesia y Cartografía Aplicada",
            semestre: "2026-II",
            nombreEstudiante: "Carlos Alberto Quispe Turpo",
            codigoEstudiante: "202099887",
            observaciones: "Ninguna.");
        const string nombreLargo = "Trípode de aluminio para nivel automático de alta precisión con extensión telescópica reforzada y estuche protector resistente al agua";
        var equipos = new List<Equipo> { CrearEquipo("TRIP-999", nombreLargo) };
        var detalles = new List<DetallePrestamo> { CrearDetalle(prestamo.Id, "TRIP-999") };

        var ruta = await generator.GenerarPdfAsync(prestamo, detalles, equipos);
        var destino = Path.Combine(DirectorioSalida(), "muestra_02_un_item_nombre_largo.pdf");
        File.Copy(ruta, destino, overwrite: true);

        Assert.True(File.Exists(destino));
    }

    [Fact]
    public async Task GenerarMuestra_CaracteresEspecialesEnObservaciones()
    {
        var generator = new ComprobantePrestamoGenerator();
        var prestamo = CrearPrestamo(
            docente: "Ing. Elmer Condori Apaza",
            curso: "Topografía I",
            semestre: "2026-I",
            nombreEstudiante: "Ana Lucía Ramos Chambi",
            codigoEstudiante: "202145678",
            observaciones: "Entregado en buen estado – falta calibrar el ‘nivel’ y revisar la “mira” … pendiente de confirmación");
        var equipos = CatalogoEquipos();
        var detalles = new List<DetallePrestamo>
        {
            CrearDetalle(prestamo.Id, "EST-001"),
            CrearDetalle(prestamo.Id, "NIV-002"),
            CrearDetalle(prestamo.Id, "CODIGO-QUE-NO-EXISTE"),
        };

        var ruta = await generator.GenerarPdfAsync(prestamo, detalles, equipos);
        var destino = Path.Combine(DirectorioSalida(), "muestra_03_caracteres_especiales.pdf");
        File.Copy(ruta, destino, overwrite: true);

        Assert.True(File.Exists(destino));
    }
}
