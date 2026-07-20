using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services.Validation;

namespace SisLabTopo.Services.Tests;

/// <summary>
/// Puerto de la lógica de <c>EquipoServiceImpl.importarDesdeExcel</c> (Java): guarda de
/// path traversal (rechaza rutas relativas y rutas con ".."), archivo inexistente, y
/// upsert por código (columnas 0-7 igual que Java, mapeadas 1-8 en ClosedXML por ser
/// 1-indexado). Usa SQLite real porque el comportamiento de upsert (existe → actualizar,
/// no existe → insertar) es justamente lo que se está verificando.
/// </summary>
public class EquipoServiceExcelImportTests : ServicesSqliteTestBase, IDisposable
{
    private readonly List<string> _archivosTemporales = new();

    private EquipoService CreateService(Data.SisLabTopoDbContext context) =>
        new(CreateEquipoRepo(context), new EquipoValidator(), NullLogger<EquipoService>.Instance);

    private string CrearExcelTemporal(params (string codigo, string denominacion, string modelo, string estado, string tipo)[] filas)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"import_test_{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("EQUIPOS");
            sheet.Cell(1, 1).Value = "CODIGO"; // fila de cabecera, se salta al importar
            var fila = 2;
            foreach (var (codigo, denominacion, modelo, estado, tipo) in filas)
            {
                sheet.Cell(fila, 1).Value = codigo;
                sheet.Cell(fila, 2).Value = denominacion;
                sheet.Cell(fila, 3).Value = modelo;
                sheet.Cell(fila, 4).Value = "MarcaX";
                sheet.Cell(fila, 5).Value = "SerieX";
                sheet.Cell(fila, 6).Value = estado;
                sheet.Cell(fila, 7).Value = tipo;
                sheet.Cell(fila, 8).Value = "Observación de prueba";
                fila++;
            }

            workbook.SaveAs(ruta);
        }

        _archivosTemporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _archivosTemporales)
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
    }

    [Fact]
    public async Task ImportarDesdeExcel_DeberiaRechazarRutaRelativa()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            service.ImportarDesdeExcelAsync("carpeta_relativa\\archivo.xlsx"));

        Assert.Equal(ErrorCode.ArchivoExcelInvalido, ex.Code);
    }

    [Fact]
    public async Task ImportarDesdeExcel_DeberiaRechazarRutaConDobleDoblePunto()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var rutaMaliciosa = Path.Combine(Path.GetTempPath(), "..", "archivo.xlsx");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            service.ImportarDesdeExcelAsync(rutaMaliciosa));

        Assert.Equal(ErrorCode.ArchivoExcelInvalido, ex.Code);
    }

    [Fact]
    public async Task ImportarDesdeExcel_DeberiaRechazarArchivoInexistente()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var rutaInexistente = Path.Combine(Path.GetTempPath(), $"no_existe_{Guid.NewGuid():N}.xlsx");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            service.ImportarDesdeExcelAsync(rutaInexistente));

        Assert.Equal(ErrorCode.ArchivoExcelInvalido, ex.Code);
    }

    [Fact]
    public async Task ImportarDesdeExcel_DeberiaInsertarEquiposNuevosYActualizarExistentes()
    {
        await using var context = CreateContext();
        var equipoRepo = CreateEquipoRepo(context);
        await equipoRepo.GuardarAsync(new Equipo
        {
            Codigo = "EQ-EXISTENTE",
            Denominacion = "Nombre viejo",
            Estado = EstadoEquipo.Malo,
            Tipo = TipoEquipo.Otros,
            Disponible = false,
            FechaRegistro = DateTime.Now
        });

        var ruta = CrearExcelTemporal(
            ("EQ-EXISTENTE", "Nombre actualizado", "ModeloX", "Bueno", "EstacionTotal"),
            ("EQ-NUEVO", "Equipo Nuevo", "ModeloY", "Nuevo", "Gps"));

        var service = CreateService(context);
        await service.ImportarDesdeExcelAsync(ruta);

        await using var verifyContext = CreateContext();
        var existente = await verifyContext.Equipos.FindAsync("EQ-EXISTENTE");
        var nuevo = await verifyContext.Equipos.FindAsync("EQ-NUEVO");

        Assert.Equal("Nombre actualizado", existente!.Denominacion);
        Assert.NotNull(nuevo);
        Assert.Equal("Equipo Nuevo", nuevo!.Denominacion);
        Assert.True(nuevo.Disponible); // importar siempre marca disponible=true, igual que Java
        Assert.Equal(TipoEquipo.Gps, nuevo.Tipo);
    }

    [Fact]
    public async Task ImportarDesdeExcel_DeberiaSaltarFilasConCodigoODenominacionVacios()
    {
        var ruta = CrearExcelTemporal(
            ("", "Sin código", "M", "Bueno", "Otros"),
            ("EQ-OK", "", "M", "Bueno", "Otros"),
            ("EQ-VALIDO", "Denominación válida", "M", "Bueno", "Otros"));

        await using var context = CreateContext();
        var service = CreateService(context);
        await service.ImportarDesdeExcelAsync(ruta);

        await using var verifyContext = CreateContext();
        var cantidad = verifyContext.Equipos.Count();
        Assert.Equal(1, cantidad);
        Assert.NotNull(await verifyContext.Equipos.FindAsync("EQ-VALIDO"));
    }
}
