using ClyvoCare.Application.DTOs;
using ClyvoCare.Infrastructure.Diagnostics;
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
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicRepository.GetAllWithCity");
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToList();
    }

    public (IReadOnlyList<Clinic> Items, int TotalCount) SearchPage(ClinicSearchRequest request)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicRepository.SearchPage");
        var search = request.Normalize();
        var query = Context.Set<Clinic>().AsNoTracking().AsQueryable();
        if (search.Nome is not null)
        {
            var name = search.Nome.ToLowerInvariant();
            query = query.Where(c => c.Name.ToLower().Contains(name));
        }
        if (search.Cnpj is not null)
            query = query.Where(c => c.Cnpj == search.Cnpj);
        if (search.CityId.HasValue)
            query = query.Where(c => c.CityId == search.CityId.Value);

        var total = query.Count();
        var descending = search.SortDirection == "desc";
        IOrderedQueryable<Clinic> ordered = search.SortBy switch
        {
            "name" => descending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "cnpj" => descending ? query.OrderByDescending(c => c.Cnpj) : query.OrderBy(c => c.Cnpj),
            "cityid" => descending ? query.OrderByDescending(c => c.CityId) : query.OrderBy(c => c.CityId),
            _ => descending ? query.OrderByDescending(c => c.Id) : query.OrderBy(c => c.Id)
        };
        var items = ordered.ThenBy(c => c.Id)
            .Skip((search.PageNumber - 1) * search.PageSize).Take(search.PageSize)
            .Include(c => c.City).ThenInclude(c => c.State).ToList();
        return (items, total);
    }

    public Clinic? GetByIdWithCity(long id)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicRepository.GetByIdWithCity");
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<Clinic> GetByCityIdWithCity(long cityId)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicRepository.GetByCityIdWithCity");
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
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicRepository.SearchWithCity");
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
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicRepository.ExistsByCnpj");
        ArgumentException.ThrowIfNullOrWhiteSpace(cnpj);

        var cnpjTrimmed = cnpj.Trim();
        var query = Context.Set<Clinic>().AsNoTracking().Where(c => c.Cnpj == cnpjTrimmed);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return query.Count() > 0;
    }
}
