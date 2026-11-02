using System.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.DTOs;
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
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicRepository.GetAllWithCity", ActivityKind.Client);
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToList();
    }

    public Clinic? GetByIdWithCity(long id)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicRepository.GetByIdWithCity", ActivityKind.Client);
        return Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<Clinic> GetByCityIdWithCity(long cityId)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicRepository.GetByCityIdWithCity", ActivityKind.Client);
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
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicRepository.SearchWithCity", ActivityKind.Client);
        return Filter(name, cnpj, null).OrderBy(c => c.Name).ThenBy(c => c.Id).ToList();
    }

    public (IReadOnlyList<Clinic> Items, int TotalCount) SearchPage(ClinicSearchRequest request)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicRepository.SearchPage", ActivityKind.Client);
        var query = Filter(request.Name, request.Cnpj, request.CityId);
        var total = query.Count();
        var ordered = (request.SortBy, request.SortDirection) switch
        {
            ("id", "asc") => query.OrderBy(c => c.Id),
            ("id", "desc") => query.OrderByDescending(c => c.Id),
            ("name", "asc") => query.OrderBy(c => c.Name),
            ("name", "desc") => query.OrderByDescending(c => c.Name),
            ("cnpj", "asc") => query.OrderBy(c => c.Cnpj),
            ("cnpj", "desc") => query.OrderByDescending(c => c.Cnpj),
            ("cityid", "asc") => query.OrderBy(c => c.CityId),
            ("cityid", "desc") => query.OrderByDescending(c => c.CityId),
            _ => throw new ArgumentException("Unsupported clinic ordering.")
        };
        if (request.SortBy != "id") ordered = ordered.ThenBy(c => c.Id);
        var items = ordered.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
        return (items, total);
    }

    private IQueryable<Clinic> Filter(string? name, string? cnpj, long? cityId)
    {
        var query = Context.Set<Clinic>()
            .Include(c => c.City)
            .ThenInclude(city => city.State)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToLowerInvariant();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var cnpjTrimmed = cnpj.Trim();
            query = query.Where(c => c.Cnpj == cnpjTrimmed);
        }

        if (cityId.HasValue) query = query.Where(c => c.CityId == cityId.Value);
        return query;
    }

    public bool ExistsByCnpj(string cnpj, long? excludeId = null)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicRepository.ExistsByCnpj", ActivityKind.Client);
        ArgumentException.ThrowIfNullOrWhiteSpace(cnpj);

        var cnpjTrimmed = cnpj.Trim();
        var query = Context.Set<Clinic>().AsNoTracking().Where(c => c.Cnpj == cnpjTrimmed);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return query.Count() > 0;
    }
}
