using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Services.Interfaces;

/// <summary>
/// Casos de uso de clínica (camada de aplicação).
/// </summary>
public interface IClinicService
{
    IReadOnlyList<ClinicResponse> GetAll();

    ClinicResponse? GetById(long id);

    IReadOnlyList<ClinicResponse> GetByCityId(long cityId);

    IReadOnlyList<ClinicResponse> Search(string? name, string? cnpj);

    ClinicResponse Create(ClinicRequest request);

    ClinicResponse Update(long id, ClinicRequest request);

    bool Delete(long id);
}
