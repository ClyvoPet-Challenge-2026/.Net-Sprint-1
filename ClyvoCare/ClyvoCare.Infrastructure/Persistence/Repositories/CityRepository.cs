using ClyvoCare.Application.Repositories;
using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório de City que traz a navegação State eagerly nas consultas.
/// </summary>
public sealed class CityRepository(ClyvoCareContext context)
    : Repository<City>(context), ICityRepository
{
    public IReadOnlyList<City> GetAllWithState()
    {
        return Context.Set<City>()
            .Include(c => c.State)
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToList();
    }

    public City? GetByIdWithState(long id)
    {
        return Context.Set<City>()
            .Include(c => c.State)
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }
}
