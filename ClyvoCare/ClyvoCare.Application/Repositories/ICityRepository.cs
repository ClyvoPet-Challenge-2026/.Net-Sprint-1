using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Repositories;

/// <summary>
/// Repositório especializado de City. Necessário porque os GETs precisam carregar a navegação State eagerly.
/// </summary>
public interface ICityRepository : IRepository<City>
{
    IReadOnlyList<City> GetAllWithState();

    City? GetByIdWithState(long id);
}
