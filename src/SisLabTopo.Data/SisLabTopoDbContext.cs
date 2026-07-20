using Microsoft.EntityFrameworkCore;
using SisLabTopo.Data.Entities;
using SisLabTopo.Domain.Models;

namespace SisLabTopo.Data;

/// <summary>
/// Contexto de EF Core / SQLite para SisLab-Topo. Reemplaza al libro Excel
/// (<c>ExcelEquipoRepository</c>/<c>ExcelPrestamoRepository</c> en Java) como motor
/// de almacenamiento, eliminando la reescritura completa del archivo en cada cambio.
/// </summary>
public class SisLabTopoDbContext : DbContext
{
    public DbSet<Equipo> Equipos => Set<Equipo>();

    public DbSet<Prestamo> Prestamos => Set<Prestamo>();

    public DbSet<DetallePrestamo> DetallesPrestamo => Set<DetallePrestamo>();

    /// <summary>Pares clave/valor de configuración (equivalente a la hoja CONFIG de Excel).</summary>
    public DbSet<ConfigEntry> ConfigEntries => Set<ConfigEntry>();

    /// <summary>Estado runtime persistido (bloqueo de login), fila única.</summary>
    public DbSet<AppState> AppStates => Set<AppState>();

    public SisLabTopoDbContext(DbContextOptions<SisLabTopoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Equipo>(entity =>
        {
            entity.ToTable("Equipos");
            entity.HasKey(e => e.Codigo);
            entity.Property(e => e.Codigo).HasMaxLength(50);
            entity.Property(e => e.Denominacion).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Modelo).HasMaxLength(200);
            entity.Property(e => e.Marca).HasMaxLength(200);
            entity.Property(e => e.Serie).HasMaxLength(200);
            entity.Property(e => e.Observacion).HasMaxLength(1000);
            // Enums almacenados como texto (no como entero) para que la base de datos
            // sea legible/depurable directamente y estable ante reordenamientos futuros
            // del enum en el código.
            entity.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<Prestamo>(entity =>
        {
            entity.ToTable("Prestamos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Docente).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Curso).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Semestre).IsRequired().HasMaxLength(50);
            entity.Property(p => p.NombreEstudiante).IsRequired().HasMaxLength(200);
            entity.Property(p => p.CodigoEstudiante).IsRequired().HasMaxLength(50);
            entity.Property(p => p.Observaciones).HasMaxLength(1000);
            entity.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);

            entity.HasMany(p => p.Detalles)
                .WithOne(d => d.Prestamo)
                .HasForeignKey(d => d.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DetallePrestamo>(entity =>
        {
            entity.ToTable("DetallesPrestamo");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.ObservacionItem).HasMaxLength(500);

            // Restrict (no cascada): un equipo referenciado en el historial de
            // préstamos no debe poder eliminarse arrastrando ese historial. La capa
            // de servicios (Fase 2) ya debe impedir eliminar un equipo con préstamos
            // activos (ErrorCode.EquipoEnPrestamoActivo); esta restricción de FK es
            // una segunda barrera a nivel de base de datos.
            entity.HasOne(d => d.Equipo)
                .WithMany(e => e.DetallesPrestamo)
                .HasForeignKey(d => d.EquipoCodigo)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConfigEntry>(entity =>
        {
            entity.ToTable("ConfigEntries");
            entity.HasKey(c => c.Clave);
        });

        modelBuilder.Entity<AppState>(entity =>
        {
            entity.ToTable("AppState");
            entity.HasKey(a => a.Id);
            entity.HasData(new AppState { Id = 1, IntentosFallidos = 0, HoraBloqueoUtc = null });
        });
    }
}
