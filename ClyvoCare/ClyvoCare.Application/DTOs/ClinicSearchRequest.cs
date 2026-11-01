using System.Text.RegularExpressions;

namespace ClyvoCare.Application.DTOs;

public record ClinicSearchRequest(int PageNumber = 1, int PageSize = 20, string SortBy = "id",
    string SortDirection = "asc", string? Name = null, string? Cnpj = null, long? CityId = null)
{
    public ClinicSearchRequest Normalize()
    {
        if (PageNumber < 1 || PageSize is < 1 or > 100 || (long)(PageNumber - 1) * PageSize > int.MaxValue)
            throw new ArgumentException("PageNumber must be positive, PageSize must be 1–100 and the offset must fit an integer.");
        var sortBy = SortBy?.Trim().ToLowerInvariant();
        var direction = SortDirection?.Trim().ToLowerInvariant();
        if (sortBy is not ("id" or "name" or "cnpj" or "cityid"))
            throw new ArgumentException("SortBy must be id, name, cnpj or cityId.");
        if (direction is not ("asc" or "desc"))
            throw new ArgumentException("SortDirection must be asc or desc.");
        if (CityId <= 0) throw new ArgumentException("CityId must be positive.");
        var name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();
        var cnpj = string.IsNullOrWhiteSpace(Cnpj) ? null : Cnpj.Trim();
        if (name?.Length > 150) throw new ArgumentException("Name must not exceed 150 characters.");
        if (cnpj is not null && !Regex.IsMatch(cnpj, @"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$"))
            throw new ArgumentException("Cnpj must use the format XX.XXX.XXX/XXXX-XX.");
        return this with { SortBy = sortBy, SortDirection = direction, Name = name, Cnpj = cnpj };
    }
}
