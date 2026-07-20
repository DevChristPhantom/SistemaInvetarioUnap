using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Verifica el contenido/estructura de los archivos .xlsx generados por
/// <see cref="ReporteService"/> (puerto de <c>ReporteServiceImpl.java</c>): encabezados
/// exactos, etiquetas en español para estado/tipo (no el nombre interno del enum),
/// "Sí"/"No" para disponibilidad, y "No devuelto" literal cuando no hay fecha de
/// devolución. Usa SQLite real porque ambos métodos leen directamente de los
/// repositorios reales.
/// </summary>
public class ReporteServiceTests : ServicesSqliteTestBase
{
    private ReporteService CreateService(Data.SisLabTopoDbContext context) =>
        new(CreateEquipoRepo(context), CreatePrestamoRepo(context), NullLogger<ReporteService>.Instance);

    [Fact]
    public async Task ExportarInventarioExcel_DeberiaEscribirEncabezadosYFilasConEtiquetasEnEspanol()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        await equipoRepo.GuardarAsync(new Equipo
        {
            Codigo = "EQ-01",
            Denominacion = "Estación Total",
            Modelo = "GTS-230",
            Marca = "Topcon",
            Serie = "999",
            Estado = EstadoEquipo.Regular,
            Tipo = TipoEquipo.EstacionTotal,
            Disponible = true,
            Observacion = "Sin novedad",
            FechaRegistro = new DateTime(2026, 1, 15)
        });

        var service = CreateService(context);
        var rutaGenerada = await service.ExportarInventarioExcelAsync();

        try
        {
            using var workbook = new XLWorkbook(rutaGenerada);
            var sheet = workbook.Worksheet("Inventario de Equipos");

            var cabeceras = new[] { "CÓDIGO", "DENOMINACIÓN", "MODELO", "MARCA", "SERIE", "ESTADO", "TIPO", "DISPONIBLE", "OBSERVACIÓN", "FECHA REGISTRO" };
            for (var i = 0; i < cabeceras.Length; i++)
            {
                Assert.Equal(cabeceras[i], sheet.Cell(1, i + 1).GetString());
            }

            Assert.Equal("EQ-01", sheet.Cell(2, 1).GetString());
            Assert.Equal("Regular", sheet.Cell(2, 6).GetString()); // etiqueta, no "Regular" del enum interno (coinciden acá, ver caso Tipo abajo)
            Assert.Equal("Estación Total", sheet.Cell(2, 7).GetString()); // etiqueta con espacio/tilde, distinta del nombre interno "EstacionTotal"
            Assert.Equal("Sí", sheet.Cell(2, 8).GetString());
        }
        finally
        {
            File.Delete(rutaGenerada);
        }
    }

    [Fact]
    public async Task ExportarHistorialExcel_DeberiaEscribirNoDevuelto_CuandoNoHayFechaDeDevolucion()
    {
        await using var context = CreateContext();
        var prestamoRepo = CreatePrestamoRepo(context);
        await prestamoRepo.GuardarAsync(new Prestamo
        {
            Id = "p-01",
            Docente = "Abdul Tacma",
            Curso = "Topografía Minera",
            Semestre = "2026-I",
            NombreEstudiante = "Juan Perez",
            CodigoEstudiante = "160244",
            FechaPrestamo = new DateTime(2026, 3, 10),
            FechaDevolucion = null,
            Estado = EstadoPrestamo.Activo,
            Observaciones = "",
            FechaRegistro = new DateTime(2026, 3, 10)
        });

        var service = CreateService(context);
        var rutaGenerada = await service.ExportarHistorialExcelAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        try
        {
            using var workbook = new XLWorkbook(rutaGenerada);
            var sheet = workbook.Worksheet("Historial de Préstamos");

            var cabeceras = new[] { "ID", "DOCENTE", "CURSO", "SEMESTRE", "ESTUDIANTE", "CÓD. ESTUDIANTE", "FECHA PRÉSTAMO", "FECHA DEVOLUCIÓN", "ESTADO", "OBSERVACIONES" };
            for (var i = 0; i < cabeceras.Length; i++)
            {
                Assert.Equal(cabeceras[i], sheet.Cell(1, i + 1).GetString());
            }

            Assert.Equal("p-01", sheet.Cell(2, 1).GetString());
            Assert.Equal("No devuelto", sheet.Cell(2, 8).GetString());
            Assert.Equal("Activo", sheet.Cell(2, 9).GetString());
        }
        finally
        {
            File.Delete(rutaGenerada);
        }
    }

    [Fact]
    public async Task ExportarHistorialExcel_DeberiaExcluirPrestamosFueraDelRangoDeFechas()
    {
        await using var context = CreateContext();
        var prestamoRepo = CreatePrestamoRepo(context);
        await prestamoRepo.GuardarAsync(new Prestamo
        {
            Id = "dentro",
            Docente = "D",
            Curso = "C",
            Semestre = "S",
            NombreEstudiante = "E",
            CodigoEstudiante = "1",
            FechaPrestamo = new DateTime(2026, 6, 1),
            Estado = EstadoPrestamo.Activo,
            FechaRegistro = new DateTime(2026, 6, 1)
        });
        await prestamoRepo.GuardarAsync(new Prestamo
        {
            Id = "fuera",
            Docente = "D",
            Curso = "C",
            Semestre = "S",
            NombreEstudiante = "E",
            CodigoEstudiante = "1",
            FechaPrestamo = new DateTime(2025, 1, 1),
            Estado = EstadoPrestamo.Activo,
            FechaRegistro = new DateTime(2025, 1, 1)
        });

        var service = CreateService(context);
        var rutaGenerada = await service.ExportarHistorialExcelAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        try
        {
            using var workbook = new XLWorkbook(rutaGenerada);
            var sheet = workbook.Worksheet("Historial de Préstamos");
            var filasUsadas = sheet.RangeUsed()!.RowsUsed().Count();

            Assert.Equal(2, filasUsadas); // 1 cabecera + 1 fila de datos ("dentro")
            Assert.Equal("dentro", sheet.Cell(2, 1).GetString());
        }
        finally
        {
            File.Delete(rutaGenerada);
        }
    }
}
