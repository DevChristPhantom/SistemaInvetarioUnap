using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Data.Import;
using SisLabTopo.Data.Repositories;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Tests;

/// <summary>
/// Prueba el importador de datos legados contra un .xlsx de prueba construido con el
/// mismo layout de columnas que <c>util.ExcelUtils.inicializarBaseDatos</c> definía en
/// la versión Java (hojas EQUIPOS, PRESTAMOS, DETALLE_PRESTAMO, CONFIG).
/// </summary>
public class LegacyExcelImporterTests : SqliteTestBase
{
    private string CrearXlsxFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"legacy_fixture_{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();

        var equipos = workbook.Worksheets.Add("EQUIPOS");
        string[] colEq = { "CODIGO", "DENOMINACION", "MODELO", "MARCA", "SERIE", "ESTADO", "TIPO", "DISPONIBLE", "OBSERVACION", "FECHA_REGISTRO" };
        for (var i = 0; i < colEq.Length; i++) equipos.Cell(1, i + 1).Value = colEq[i];
        equipos.Cell(2, 1).Value = "EQ-LEG-01";
        equipos.Cell(2, 2).Value = "Estación Total Legada";
        equipos.Cell(2, 3).Value = "GTS-230";
        equipos.Cell(2, 4).Value = "Topcon";
        equipos.Cell(2, 5).Value = "SN-001";
        equipos.Cell(2, 6).Value = "BUENO";
        equipos.Cell(2, 7).Value = "ESTACION_TOTAL";
        equipos.Cell(2, 8).Value = true;
        equipos.Cell(2, 9).Value = "Importado";
        equipos.Cell(2, 10).Value = new DateTime(2025, 3, 1, 8, 0, 0);

        var prestamos = workbook.Worksheets.Add("PRESTAMOS");
        string[] colPr = { "ID", "DOCENTE", "CURSO", "SEMESTRE", "NOMBRE_ESTUDIANTE", "CODIGO_ESTUDIANTE", "FECHA_PRESTAMO", "FECHA_DEVOLUCION", "ESTADO", "OBSERVACIONES", "FECHA_REGISTRO" };
        for (var i = 0; i < colPr.Length; i++) prestamos.Cell(1, i + 1).Value = colPr[i];
        prestamos.Cell(2, 1).Value = "PR-LEG-01";
        prestamos.Cell(2, 2).Value = "Abdul Tacma Fernández";
        prestamos.Cell(2, 3).Value = "Topografía I";
        prestamos.Cell(2, 4).Value = "2025-II";
        prestamos.Cell(2, 5).Value = "María López";
        prestamos.Cell(2, 6).Value = "2019-54321";
        prestamos.Cell(2, 7).Value = new DateTime(2025, 3, 1, 9, 0, 0);
        // FECHA_DEVOLUCION queda vacía a propósito (préstamo activo)
        prestamos.Cell(2, 9).Value = "ACTIVO";
        prestamos.Cell(2, 10).Value = "Ninguna";
        prestamos.Cell(2, 11).Value = new DateTime(2025, 3, 1, 9, 0, 0);

        var detalle = workbook.Worksheets.Add("DETALLE_PRESTAMO");
        string[] colDet = { "ID", "PRESTAMO_ID", "EQUIPO_CODIGO", "OBSERVACION_ITEM", "DEVUELTO" };
        for (var i = 0; i < colDet.Length; i++) detalle.Cell(1, i + 1).Value = colDet[i];
        detalle.Cell(2, 1).Value = "DET-LEG-01";
        detalle.Cell(2, 2).Value = "PR-LEG-01";
        detalle.Cell(2, 3).Value = "EQ-LEG-01";
        detalle.Cell(2, 4).Value = "Sin daños";
        detalle.Cell(2, 5).Value = false;

        var config = workbook.Worksheets.Add("CONFIG");
        config.Cell(1, 1).Value = "CLAVE";
        config.Cell(1, 2).Value = "VALOR";
        config.Cell(2, 1).Value = "institucion.nombre";
        config.Cell(2, 2).Value = "Universidad Nacional del Altiplano";
        config.Cell(3, 1).Value = "app.version";
        config.Cell(3, 2).Value = "1.0.0";

        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public async Task ImportarSiCorresponde_ConArchivoInexistente_DeberiaOmitir()
    {
        await using var context = CreateContext();
        var importer = new LegacyExcelImporter(context, NullLogger<LegacyExcelImporter>.Instance);

        var resultado = await importer.ImportarSiCorrespondeAsync(
            Path.Combine(Path.GetTempPath(), $"no_existe_{Guid.NewGuid():N}.xlsx"));

        Assert.False(resultado.Importado);
    }

    [Fact]
    public async Task ImportarSiCorresponde_ConBaseVacia_DeberiaImportarLasCuatroHojas()
    {
        var fixturePath = CrearXlsxFixture();
        try
        {
            await using var context = CreateContext();
            var importer = new LegacyExcelImporter(context, NullLogger<LegacyExcelImporter>.Instance);

            var resultado = await importer.ImportarSiCorrespondeAsync(fixturePath);

            Assert.True(resultado.Importado);
            Assert.Equal(1, resultado.Equipos);
            Assert.Equal(1, resultado.Prestamos);
            Assert.Equal(1, resultado.Detalles);
            Assert.Equal(2, resultado.ConfigEntries);

            var equipoRepo = new EquipoRepository(context, NullLogger<EquipoRepository>.Instance);
            var equipo = await equipoRepo.BuscarPorCodigoAsync("EQ-LEG-01");
            Assert.NotNull(equipo);
            Assert.Equal("Estación Total Legada", equipo!.Denominacion);
            Assert.Equal(EstadoEquipo.Bueno, equipo.Estado);
            Assert.Equal(TipoEquipo.EstacionTotal, equipo.Tipo);
            Assert.True(equipo.Disponible);

            var prestamoRepo = new PrestamoRepository(context, NullLogger<PrestamoRepository>.Instance);
            var prestamo = await prestamoRepo.BuscarPorIdAsync("PR-LEG-01");
            Assert.NotNull(prestamo);
            Assert.Equal("María López", prestamo!.NombreEstudiante);
            Assert.Equal(EstadoPrestamo.Activo, prestamo.Estado);
            Assert.Null(prestamo.FechaDevolucion);

            var detalles = await prestamoRepo.ObtenerDetalleAsync("PR-LEG-01");
            Assert.Single(detalles);
            Assert.Equal("EQ-LEG-01", detalles[0].EquipoCodigo);

            var valorConfig = await prestamoRepo.ObtenerConfigAsync("institucion.nombre");
            Assert.Equal("Universidad Nacional del Altiplano", valorConfig);
        }
        finally
        {
            if (File.Exists(fixturePath))
            {
                File.Delete(fixturePath);
            }
        }
    }

    [Fact]
    public async Task ImportarSiCorresponde_ConBaseQueYaTieneDatos_DeberiaOmitir()
    {
        var fixturePath = CrearXlsxFixture();
        try
        {
            await using var context = CreateContext();
            context.Equipos.Add(new Equipo
            {
                Codigo = "EQ-EXISTENTE",
                Denominacion = "Ya estaba",
                Estado = EstadoEquipo.Bueno,
                Tipo = TipoEquipo.Otros,
                FechaRegistro = DateTime.Now
            });
            await context.SaveChangesAsync();

            var importer = new LegacyExcelImporter(context, NullLogger<LegacyExcelImporter>.Instance);
            var resultado = await importer.ImportarSiCorrespondeAsync(fixturePath);

            Assert.False(resultado.Importado);

            var equipoRepo = new EquipoRepository(context, NullLogger<EquipoRepository>.Instance);
            var equipoLegado = await equipoRepo.BuscarPorCodigoAsync("EQ-LEG-01");
            Assert.Null(equipoLegado); // no se importó nada
        }
        finally
        {
            if (File.Exists(fixturePath))
            {
                File.Delete(fixturePath);
            }
        }
    }
}
