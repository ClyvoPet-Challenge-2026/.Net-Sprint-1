using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Services.Implementations;

/// <summary>
/// Orquestra os casos de uso de clínica.
/// </summary>
public sealed class ClinicService(
    IClinicRepository clinicRepository,
    IRepository<City> cityRepository) : IClinicService
{
    /// <inheritdoc />
    public IReadOnlyList<ClinicResponse> GetAll()
    {
        return clinicRepository
            .GetAllWithCity()
            .Select(ClinicResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public ClinicResponse? GetById(long id)
    {
        var clinic = clinicRepository.GetByIdWithCity(id);
        return clinic is null ? null : ClinicResponse.FromDomain(clinic);
    }

    /// <inheritdoc />
    public IReadOnlyList<ClinicResponse> GetByCityId(long cityId)
    {
        EnsureCityExists(cityId);

        return clinicRepository
            .GetByCityIdWithCity(cityId)
            .Select(ClinicResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ClinicResponse> Search(string? name, string? cnpj)
    {
        return clinicRepository
            .SearchWithCity(name, cnpj)
            .Select(ClinicResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public ClinicResponse Create(ClinicRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureCityExists(request.CityId);
        EnsureCnpjAvailable(request.Cnpj);

        var clinic = request.ToDomain();
        var saved = clinicRepository.Add(clinic);

        var withCity = clinicRepository.GetByIdWithCity(saved.Id)!;
        return ClinicResponse.FromDomain(withCity);
    }

    /// <inheritdoc />
    public ClinicResponse Update(long id, ClinicRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clinic = clinicRepository.GetById(id)
            ?? throw new KeyNotFoundException($"Clínica com ID {id} não encontrada.");

        EnsureCityExists(request.CityId);
        EnsureCnpjAvailable(request.Cnpj, excludeId: id);

        clinic.Update(request.Name, request.Cnpj, request.CityId, request.Phone);
        clinicRepository.Update(clinic);

        var withCity = clinicRepository.GetByIdWithCity(id)!;
        return ClinicResponse.FromDomain(withCity);
    }

    /// <inheritdoc />
    public bool Delete(long id)
    {
        return clinicRepository.Delete(id);
    }

    private void EnsureCityExists(long cityId)
    {
        if (!cityRepository.ExistsById(cityId))
            throw new KeyNotFoundException($"Cidade com ID {cityId} não encontrada.");
    }

    private void EnsureCnpjAvailable(string cnpj, long? excludeId = null)
    {
        if (clinicRepository.ExistsByCnpj(cnpj, excludeId))
            throw new InvalidOperationException($"Já existe uma clínica cadastrada com o CNPJ {cnpj}.");
    }
}
