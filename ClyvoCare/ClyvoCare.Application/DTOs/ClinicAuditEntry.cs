namespace ClyvoCare.Application.DTOs;

/// <summary>Metadados de uma alteração confirmada no cadastro de clínicas.</summary>
public sealed record ClinicAuditEntry(string Id, long ClinicId, string Operation,
    long ActorId, DateTime OccurredAtUtc, string? TraceId);
