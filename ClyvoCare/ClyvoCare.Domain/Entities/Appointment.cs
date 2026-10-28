using ClyvoCare.Domain.Common;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Domain.Entities;

/// <summary>Agendamento no contexto clínico compartilhado (TB_HEA_CLINICAL_EVENT).</summary>
public sealed class Appointment : BaseEntity
{
    private static readonly HashSet<string> EventTypes =
        ["CONSULTATION", "VACCINATION", "SURGERY", "FOLLOW_UP", "EXAM"];

    public long PetId { get; private set; }
    public long ClinicId { get; private set; }
    public string EventType { get; private set; } = null!;
    public DateTime EventDate { get; private set; }
    public string Status { get; private set; } = null!;
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public PetReference Pet { get; private set; } = null!;
    public Clinic Clinic { get; private set; } = null!;

    private Appointment() { }

    public static Appointment Create(long petId, long clinicId, string eventType,
        DateTimeOffset scheduledAt, string reason, string? notes, DateTimeOffset now)
    {
        if (petId <= 0 || clinicId <= 0)
            throw new DomainException("PetId e ClinicId devem ser positivos.");
        if (string.IsNullOrWhiteSpace(eventType) || !EventTypes.Contains(eventType))
            throw new DomainException("Tipo de atendimento inválido.");
        // Oracle TIMESTAMP(6): normaliza a precisao antes de validar e consultar duplicidade.
        var utcDate = new DateTime(scheduledAt.UtcDateTime.Ticks / 10 * 10, DateTimeKind.Utc);
        if (new DateTimeOffset(utcDate) <= now)
            throw new DomainException("O agendamento deve ser para uma data e horário futuros.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 150)
            throw new DomainException("O motivo é obrigatório e deve ter no máximo 150 caracteres.");
        if (notes?.Length > 1000)
            throw new DomainException("As observações devem ter no máximo 1000 caracteres.");

        return new Appointment
        {
            PetId = petId,
            ClinicId = clinicId,
            EventType = eventType,
            EventDate = utcDate,
            Status = "SCHEDULED",
            Reason = reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }
}
