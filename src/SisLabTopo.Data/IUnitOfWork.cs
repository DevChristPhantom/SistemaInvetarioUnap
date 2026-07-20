using Microsoft.EntityFrameworkCore.Storage;

namespace SisLabTopo.Data;

/// <summary>
/// Unidad de trabajo mínima sobre <see cref="SisLabTopoDbContext"/>.
///
/// Decisión de diseño: los repositorios (<see cref="Repositories.EquipoRepository"/>,
/// <see cref="Repositories.PrestamoRepository"/>) reciben el <see cref="SisLabTopoDbContext"/>
/// por inyección de dependencias con ciclo de vida "Scoped". Eso significa que, dentro
/// de un mismo scope de DI, un <c>IEquipoRepository</c> y un <c>IPrestamoRepository</c>
/// comparten la MISMA instancia de <see cref="SisLabTopoDbContext"/> y por lo tanto la
/// misma conexión subyacente. Esto es justo lo que necesita la Fase 2 para
/// <c>RegistrarPrestamo</c>: inyectar ambos repositorios más un <see cref="IUnitOfWork"/>,
/// llamar <see cref="BeginTransactionAsync"/>, invocar los métodos de guardado de ambos
/// repositorios (equipo + préstamo + detalles) y luego confirmar o revertir una única
/// transacción real de EF Core — sin necesidad de un Unit-of-Work más elaborado que
/// coordine múltiples DbContexts.
/// </summary>
public interface IUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SisLabTopoDbContext _context;

    public UnitOfWork(SisLabTopoDbContext context)
    {
        _context = context;
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => _context.Database.BeginTransactionAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
