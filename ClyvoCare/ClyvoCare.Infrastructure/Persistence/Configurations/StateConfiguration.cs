using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClyvoCare.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF para a entidade State (TB_CAD_STATE).
/// Read-only: a tabela é escrita pela API Java, o C# apenas consulta.
/// </summary>
public sealed class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("TB_CAD_STATE");

        builder.HasKey(s => s.Id);

        builder
            .Property(s => s.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder
            .Property(s => s.Name)
            .HasColumnName("NAME")
            .HasMaxLength(50)
            .IsRequired();

        builder
            .Property(s => s.UF)
            .HasColumnName("UF")
            .HasMaxLength(2)
            .IsRequired();
    }
}
