using System.ComponentModel.DataAnnotations;

namespace ClyvoCare.Application.DTOs;

public record AppointmentRequest(
    [param:Range(1, long.MaxValue)] long PetId,
    [param:Range(1, long.MaxValue)] long ClinicId,
    [param:Required]
    [param:RegularExpression("^(CONSULTATION|VACCINATION|SURGERY|FOLLOW_UP|EXAM)$")]
    string EventType,
    [param:Required] DateTimeOffset? ScheduledAt,
    [param:Required]
    [param:StringLength(150)] string Reason,
    [param:StringLength(1000)] string? Notes);
