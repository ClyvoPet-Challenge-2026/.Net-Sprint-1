using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Services.Interfaces;

/// <summary>
/// Casos de uso de leitura para cidades.
/// O lado C# do ClyvoCare apenas lê esta tabela, que é escrita pela API Java.
/// </summary>
public interface ICityService
{
    IReadOnlyList<CityResponse> GetAll();

    CityResponse? GetById(long id);
}
