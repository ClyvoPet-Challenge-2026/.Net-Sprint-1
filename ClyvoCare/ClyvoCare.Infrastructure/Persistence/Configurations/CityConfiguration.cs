using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClyvoCare.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF para a entidade City (TB_CAD_CITY).
/// Read-only: a tabela é escrita pela API Java, o C# apenas consulta.
/// </summary>
public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("TB_CAD_CITY");

        builder.HasKey(c => c.Id);

        builder
            .Property(c => c.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder
            .Property(c => c.Name)
            .HasColumnName("NAME")
            .HasMaxLength(100)
            .IsRequired();

        builder
            .Property(c => c.StateId)
            .HasColumnName("STATE_ID")
            .IsRequired();

        builder
            .HasOne(c => c.State)
            .WithMany()
            .HasForeignKey(c => c.StateId)
            .IsRequired();
    }
}
