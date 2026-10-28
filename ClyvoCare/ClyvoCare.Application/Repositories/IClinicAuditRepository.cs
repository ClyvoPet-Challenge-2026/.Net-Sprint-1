using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Repositories;

public interface IClinicAuditRepository
{
    /// <summary>Retorna false e registra falhas de disponibilidade; não desfaz a gravação relacional.</summary>
    bool TryAdd(ClinicAuditEntry entry);
}
