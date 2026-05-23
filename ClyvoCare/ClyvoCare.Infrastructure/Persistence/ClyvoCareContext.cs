using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core do projeto ClyvoCare.
/// Configura o mapeamento das entidades para o banco Oracle FIAP compartilhado com a API Java.
/// </summary>
public class ClyvoCareContext(DbContextOptions<ClyvoCareContext> options) : DbContext(options)
{
    public DbSet<State> States { get; set; }

    public DbSet<City> Cities { get; set; }

    public DbSet<Clinic> Clinics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClyvoCareContext).Assembly);
    }
}
