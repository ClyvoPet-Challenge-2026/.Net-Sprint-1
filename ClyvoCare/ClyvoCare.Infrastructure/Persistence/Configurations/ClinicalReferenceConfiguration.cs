using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClyvoCare.Infrastructure.Persistence.Configurations;

public sealed class OwnerReferenceConfiguration : IEntityTypeConfiguration<OwnerReference>
{
    public void Configure(EntityTypeBuilder<OwnerReference> builder)
    {
        builder.ToTable("TB_CAD_OWNER", table => table.ExcludeFromMigrations());
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(o => o.Email).HasColumnName("EMAIL").HasMaxLength(150).IsUnicode(false).IsRequired();
        builder.Property(o => o.RoleName).HasColumnName("ROLE_NAME").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(o => o.Enabled).HasColumnName("ENABLED")
            .HasMaxLength(1).IsFixedLength().IsUnicode(false).IsRequired();
    }
}

public sealed class PetReferenceConfiguration : IEntityTypeConfiguration<PetReference>
{
    public void Configure(EntityTypeBuilder<PetReference> builder)
    {
        builder.ToTable("TB_CAD_PET", table => table.ExcludeFromMigrations());
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.OwnerId).HasColumnName("OWNER_ID");
        builder.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId)
            .HasConstraintName("FK_PET_OWNER").OnDelete(DeleteBehavior.Restrict);
    }
}
