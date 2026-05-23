using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Services.Interfaces;

/// <summary>
/// Casos de uso de leitura para estados brasileiros.
/// O lado C# do ClyvoCare apenas lê esta tabela, que é escrita pela API Java.
/// </summary>
public interface IStateService
{
    IReadOnlyList<StateResponse> GetAll();

    StateResponse? GetById(long id);
}
