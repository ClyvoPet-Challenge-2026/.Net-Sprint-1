using System.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
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
        using var activity = PersistenceTelemetry.Source.StartActivity("CityRepository.GetAllWithState", ActivityKind.Client);
        return Context.Set<City>()
            .Include(c => c.State)
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToList();
    }

    public City? GetByIdWithState(long id)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("CityRepository.GetByIdWithState", ActivityKind.Client);
        return Context.Set<City>()
            .Include(c => c.State)
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }
}
