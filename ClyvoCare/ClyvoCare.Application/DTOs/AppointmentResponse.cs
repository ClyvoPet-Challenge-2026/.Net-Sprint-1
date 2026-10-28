using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.DTOs;

public record AppointmentResponse(long Id, long PetId, long ClinicId, string EventType,
    DateTimeOffset ScheduledAt, string Status, string? Reason, string? Notes)
{
    public static AppointmentResponse FromDomain(Appointment appointment) =>
        new(appointment.Id, appointment.PetId, appointment.ClinicId, appointment.EventType,
            new DateTimeOffset(DateTime.SpecifyKind(appointment.EventDate, DateTimeKind.Utc)),
            appointment.Status, appointment.Reason, appointment.Notes);
}
