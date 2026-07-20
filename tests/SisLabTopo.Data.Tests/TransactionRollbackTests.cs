using Microsoft.EntityFrameworkCore;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data.Tests;

/// <summary>
/// Verifica que las transacciones reales de EF Core hacen rollback correctamente
/// ante una violación de restricción (código de equipo duplicado) — la propiedad
/// concreta que la Fase 2 necesitará para envolver equipo+préstamo+detalles en una
/// única transacción atómica (ver <see cref="IUnitOfWork"/>). Con la versión Java
/// (Excel reescrito completo por operación) este escenario no existía como tal: aquí
/// se prueba explícitamente porque es la garantía central que justifica haber migrado
/// a un motor transaccional real.
/// </summary>
public class TransactionRollbackTests : SqliteTestBase
{
    [Fact]
    public async Task Transaccion_ConMultiplesOperaciones_DebeRevertirTodoAlFallarUnaRestriccionDeClaveDuplicada()
    {
        await using var context = CreateContext();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Equipos.Add(new Equipo
            {
                Codigo = "EQ-TX-01",
                Denominacion = "Primero",
                Estado = EstadoEquipo.Bueno,
                Tipo = TipoEquipo.Otros,
                FechaRegistro = DateTime.Now
            });
            await context.SaveChangesAsync(); // se persiste dentro de la transacción abierta, aún no confirmada

            // Se libera el tracking local: si no, EF Core rechazaría el segundo Add()
            // en memoria (identity map) antes de siquiera llegar a la base de datos,
            // y no estaríamos probando la restricción real de SQLite.
            context.ChangeTracker.Clear();

            context.Equipos.Add(new Equipo
            {
                Codigo = "EQ-TX-01", // mismo código -> viola la clave primaria
                Denominacion = "Segundo",
                Estado = EstadoEquipo.Bueno,
                Tipo = TipoEquipo.Otros,
                FechaRegistro = DateTime.Now
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

            await transaction.RollbackAsync();
        }

        // Con un DbContext nuevo, para no depender del tracking en memoria del primero:
        // el rollback debe haber revertido también la PRIMERA inserción, ya que ambas
        // ocurrieron dentro de la misma transacción explícita.
        await using var verifyContext = CreateContext();
        var cantidad = await verifyContext.Equipos.CountAsync(e => e.Codigo == "EQ-TX-01");

        Assert.Equal(0, cantidad);
    }

    [Fact]
    public async Task Transaccion_Confirmada_DebePersistirTodasLasOperaciones()
    {
        await using (var context = CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            context.Equipos.Add(new Equipo
            {
                Codigo = "EQ-TX-02",
                Denominacion = "Equipo A",
                Estado = EstadoEquipo.Bueno,
                Tipo = TipoEquipo.Otros,
                FechaRegistro = DateTime.Now
            });
            await context.SaveChangesAsync();

            context.Equipos.Add(new Equipo
            {
                Codigo = "EQ-TX-03",
                Denominacion = "Equipo B",
                Estado = EstadoEquipo.Bueno,
                Tipo = TipoEquipo.Otros,
                FechaRegistro = DateTime.Now
            });
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        await using var verifyContext = CreateContext();
        var cantidad = await verifyContext.Equipos.CountAsync(e =>
            e.Codigo == "EQ-TX-02" || e.Codigo == "EQ-TX-03");

        Assert.Equal(2, cantidad);
    }
}
