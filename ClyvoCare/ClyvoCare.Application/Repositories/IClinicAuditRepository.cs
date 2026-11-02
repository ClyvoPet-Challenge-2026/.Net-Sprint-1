using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Repositories;

public interface IClinicAuditRepository
{
    bool TryAdd(ClinicAuditEntry entry);
}
