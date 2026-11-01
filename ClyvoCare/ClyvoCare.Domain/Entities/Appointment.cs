using ClyvoCare.Domain.Common;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Domain.Entities;

public sealed class Appointment : BaseEntity
{
    public long PetId { get; private set; }
    public long ClinicId { get; private set; }
    public string EventType { get; private set; } = null!;
    public DateTime EventDate { get; private set; }
    public string Status { get; private set; } = null!;
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public PetReference Pet { get; private set; } = null!;
    public Clinic Clinic { get; private set; } = null!;

    private Appointment()
    {
    }

    public static Appointment Create(long petId, long clinicId, string eventType,
        DateTimeOffset scheduledAt, string reason, string? notes, DateTimeOffset now)
    {
        if (petId <= 0 || clinicId <= 0)
            throw new DomainException("PetId and ClinicId must be positive.");

        if (eventType is not ("CONSULTATION" or "VACCINATION" or "SURGERY" or "FOLLOW_UP" or "EXAM"))
            throw new DomainException("Invalid appointment event type.");

        // Oracle TIMESTAMP(6) stores microseconds; validate the instant that will be persisted.
        var utc = scheduledAt.UtcDateTime;
        var date = utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
        if (date <= now.UtcDateTime)
            throw new DomainException("Appointments must be scheduled in the future.");

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 150)
            throw new DomainException("Reason is required and must not exceed 150 characters.");

        if (notes?.Length > 1000)
            throw new DomainException("Notes must not exceed 1000 characters.");

        return new Appointment
        {
            PetId = petId,
            ClinicId = clinicId,
            EventType = eventType,
            EventDate = date,
            Status = "SCHEDULED",
            Reason = reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }
}
