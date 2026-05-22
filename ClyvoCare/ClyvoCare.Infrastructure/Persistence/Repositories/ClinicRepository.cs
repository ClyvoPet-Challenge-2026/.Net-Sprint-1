using ClyvoCare.Application.Repositories;
using ClyvoCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IClinicRepository"/>.
/// </summary>
public sealed class ClinicRepository(ClyvoCareContext context)
    : Repository<Clinic>(context), IClinicRepository
{
    public IReadOnlyList<Clinic> GetAllWithCity()
    {
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToList();
    }

    public Clinic? GetByIdWithCity(long id)
    {
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<Clinic> GetByCityIdWithCity(long cityId)
    {
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .Where(c => c.CityId == cityId)
            .OrderBy(c => c.Name)
            .ToList();
    }

    public IReadOnlyList<Clinic> SearchWithCity(string? name, string? cnpj)
    {
        var query = Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{name.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Name.ToLower(), pattern.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var cnpjTrimmed = cnpj.Trim();
            query = query.Where(c => c.Cnpj == cnpjTrimmed);
        }

        return query.OrderBy(c => c.Name).ToList();
    }

    public bool ExistsByCnpj(string cnpj, long? excludeId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cnpj);

        var cnpjTrimmed = cnpj.Trim();
        var query = Context.Set<Clinic>().AsNoTracking().Where(c => c.Cnpj == cnpjTrimmed);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return query.Count() > 0;
    }
}
