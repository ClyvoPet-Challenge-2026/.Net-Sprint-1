using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClyvoCare.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF para a entidade Clinic (TB_CAD_CLINIC).
/// </summary>
public sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.ToTable("TB_CAD_CLINIC");

        builder.HasKey(c => c.Id);

        builder
            .Property(c => c.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder
            .Property(c => c.Name)
            .HasColumnName("NAME")
            .HasMaxLength(150)
            .IsRequired();

        builder
            .Property(c => c.Cnpj)
            .HasColumnName("CNPJ")
            .HasMaxLength(18)
            .IsRequired();

        builder
            .HasIndex(c => c.Cnpj)
            .IsUnique();

        builder
            .Property(c => c.CityId)
            .HasColumnName("CITY_ID")
            .IsRequired();

        builder
            .Property(c => c.Phone)
            .HasColumnName("PHONE")
            .HasMaxLength(20);

        builder
            .HasOne(c => c.City)
            .WithMany()
            .HasForeignKey(c => c.CityId)
            .IsRequired();
    }
}
