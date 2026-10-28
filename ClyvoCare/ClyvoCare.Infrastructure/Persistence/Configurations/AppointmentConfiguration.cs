using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClyvoCare.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("TB_HEA_CLINICAL_EVENT");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.PetId).HasColumnName("PET_ID").IsRequired();
        builder.Property(a => a.ClinicId).HasColumnName("CLINIC_ID").IsRequired();
        builder.Property(a => a.EventType).HasColumnName("EVENT_TYPE").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(a => a.EventDate).HasColumnName("EVENT_DATE").HasColumnType("TIMESTAMP(6)").HasPrecision(6).IsRequired();
        builder.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(a => a.Reason).HasColumnName("REASON").HasMaxLength(150);
        builder.Property(a => a.Notes).HasColumnName("NOTES").HasMaxLength(1000);
        builder.HasIndex(a => a.ClinicId).HasDatabaseName("IX_EVENT_CLINIC");
        builder.HasIndex(a => new { a.PetId, a.EventDate }).IsUnique().HasDatabaseName("UX_EVENT_PET_DATE");
        builder.HasOne(a => a.Pet).WithMany().HasForeignKey(a => a.PetId).HasConstraintName("FK_EVENT_PET").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Clinic).WithMany().HasForeignKey(a => a.ClinicId).HasConstraintName("FK_EVENT_CLINIC").OnDelete(DeleteBehavior.Restrict);
    }
}
