using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Interfaces;

namespace ClyvoCare.Application.Services.Implementations;

/// <summary>
/// Orquestra os casos de uso de leitura de cidade.
/// Usa o repositório especializado para trazer o estado aninhado nas consultas.
/// </summary>
public sealed class CityService(ICityRepository cityRepository) : ICityService
{
    /// <inheritdoc />
    public IReadOnlyList<CityResponse> GetAll()
    {
        return cityRepository
            .GetAllWithState()
            .Select(CityResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public CityResponse? GetById(long id)
    {
        var city = cityRepository.GetByIdWithState(id);
        return city is null ? null : CityResponse.FromDomain(city);
    }
}
