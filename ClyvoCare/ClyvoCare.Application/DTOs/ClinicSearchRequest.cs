namespace ClyvoCare.Application.DTOs;

public sealed record ClinicSearchRequest(int PageNumber = 1, int PageSize = 20,
    string SortBy = "id", string SortDirection = "asc", string? Nome = null,
    string? Cnpj = null, long? CityId = null)
{
    public ClinicSearchRequest Normalize()
    {
        if (PageNumber < 1 || PageSize < 1 || PageSize > 100
            || (long)(PageNumber - 1) * PageSize > int.MaxValue)
            throw new ArgumentException("Paginação inválida: pageNumber >= 1 e pageSize entre 1 e 100.");
        if (CityId is <= 0)
            throw new ArgumentException("cityId deve ser positivo.");

        var sortBy = SortBy?.Trim().ToLowerInvariant();
        var direction = SortDirection?.Trim().ToLowerInvariant();
        if (sortBy is not ("id" or "name" or "cnpj" or "cityid"))
            throw new ArgumentException("sortBy deve ser id, name, cnpj ou cityId.");
        if (direction is not ("asc" or "desc"))
            throw new ArgumentException("sortDirection deve ser asc ou desc.");

        return this with { SortBy = sortBy, SortDirection = direction,
            Nome = string.IsNullOrWhiteSpace(Nome) ? null : Nome.Trim(),
            Cnpj = string.IsNullOrWhiteSpace(Cnpj) ? null : Cnpj.Trim() };
    }
}
