using ClyvoCare.Domain.Entities;
using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Repositories;

/// <summary>
/// Repositório de Clinic com métodos que já carregam a cidade aninhada.
/// </summary>
public interface IClinicRepository : IRepository<Clinic>
{
    IReadOnlyList<Clinic> GetAllWithCity();

    Clinic? GetByIdWithCity(long id);

    IReadOnlyList<Clinic> GetByCityIdWithCity(long cityId);

    IReadOnlyList<Clinic> SearchWithCity(string? name, string? cnpj);

    bool ExistsByCnpj(string cnpj, long? excludeId = null);

    (IReadOnlyList<Clinic> Items, int TotalCount) SearchPage(ClinicSearchRequest request);
}
