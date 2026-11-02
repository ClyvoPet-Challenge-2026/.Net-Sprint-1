namespace ClyvoCare.Application.DTOs;

public sealed record ClinicAuditEntry(long ClinicId, string Operation, long ActorId,
    DateTime OccurredAtUtc, string? TraceId);
