using System.Diagnostics;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Application.Diagnostics;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Services.Implementations;

/// <summary>
/// Orquestra os casos de uso de clínica.
/// </summary>
public sealed class ClinicService(
    IClinicRepository clinicRepository,
    IRepository<City> cityRepository, IClinicAuditRepository auditRepository,
    ICurrentUser currentUser, TimeProvider timeProvider) : IClinicService
{
    /// <inheritdoc />
    public IReadOnlyList<ClinicResponse> GetAll()
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.GetAll");
        return clinicRepository
            .GetAllWithCity()
            .Select(ClinicResponse.FromDomain)
            .ToList();
    }

    public PagedResult<ClinicResponse> SearchPage(ClinicSearchRequest request)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.SearchPage");
        ArgumentNullException.ThrowIfNull(request);
        var normalized = request.Normalize();
        var (items, total) = clinicRepository.SearchPage(normalized);
        return new PagedResult<ClinicResponse>(items.Select(ClinicResponse.FromDomain).ToList(),
            normalized.PageNumber, normalized.PageSize, total);
    }

    /// <inheritdoc />
    public ClinicResponse? GetById(long id)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.GetById");
        var clinic = clinicRepository.GetByIdWithCity(id);
        return clinic is null ? null : ClinicResponse.FromDomain(clinic);
    }

    /// <inheritdoc />
    public IReadOnlyList<ClinicResponse> GetByCityId(long cityId)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.GetByCityId");
        EnsureCityExists(cityId);

        return clinicRepository
            .GetByCityIdWithCity(cityId)
            .Select(ClinicResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ClinicResponse> Search(string? name, string? cnpj)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.Search");
        return clinicRepository
            .SearchWithCity(name, cnpj)
            .Select(ClinicResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public ClinicResponse Create(ClinicRequest request)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.Create");
        ArgumentNullException.ThrowIfNull(request);

        EnsureCityExists(request.CityId);
        EnsureCnpjAvailable(request.Cnpj);

        var actorId = currentUser.OwnerId;
        var clinic = request.ToDomain();
        var saved = clinicRepository.Add(clinic);
        RecordAudit(saved.Id, "Created", actorId);

        var withCity = clinicRepository.GetByIdWithCity(saved.Id)!;
        return ClinicResponse.FromDomain(withCity);
    }

    /// <inheritdoc />
    public ClinicResponse Update(long id, ClinicRequest request)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.Update");
        ArgumentNullException.ThrowIfNull(request);

        var clinic = clinicRepository.GetById(id)
            ?? throw new KeyNotFoundException($"Clínica com ID {id} não encontrada.");

        EnsureCityExists(request.CityId);
        EnsureCnpjAvailable(request.Cnpj, excludeId: id);

        var actorId = currentUser.OwnerId;
        clinic.Update(request.Name, request.Cnpj, request.CityId, request.Phone);
        clinicRepository.Update(clinic);
        RecordAudit(id, "Updated", actorId);

        var withCity = clinicRepository.GetByIdWithCity(id)!;
        return ClinicResponse.FromDomain(withCity);
    }

    /// <inheritdoc />
    public bool Delete(long id)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("ClinicService.Delete");
        var actorId = currentUser.OwnerId;
        var deleted = clinicRepository.Delete(id);
        if (deleted)
            RecordAudit(id, "Deleted", actorId);
        return deleted;
    }

    private void RecordAudit(long clinicId, string operation, long actorId)
    {
        auditRepository.TryAdd(new ClinicAuditEntry(Guid.NewGuid().ToString("N"), clinicId,
            operation, actorId, timeProvider.GetUtcNow().UtcDateTime,
            Activity.Current?.TraceId.ToString()));
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
