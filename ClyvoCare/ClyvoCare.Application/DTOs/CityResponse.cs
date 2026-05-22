using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.DTOs;

/// <summary>
/// DTO de resposta para cidade, com o estado aninhado.
/// </summary>
public record CityResponse(long Id, string Name, StateResponse State)
{
    /// <summary>
    /// Mapeia <see cref="City"/> para DTO. Requer que a navegação State esteja carregada.
    /// </summary>
    public static CityResponse FromDomain(City city) =>
        new(city.Id, city.Name, StateResponse.FromDomain(city.State));
}
