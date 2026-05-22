using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Services.Implementations;

/// <summary>
/// Orquestra os casos de uso de leitura de estado.
/// </summary>
public sealed class StateService(IRepository<State> stateRepository) : IStateService
{
    /// <inheritdoc />
    public IReadOnlyList<StateResponse> GetAll()
    {
        return stateRepository
            .GetAll()
            .Select(StateResponse.FromDomain)
            .ToList();
    }

    /// <inheritdoc />
    public StateResponse? GetById(long id)
    {
        var state = stateRepository.GetById(id);
        return state is null ? null : StateResponse.FromDomain(state);
    }
}
