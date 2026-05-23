using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.DTOs;

/// <summary>
/// DTO de resposta para estado brasileiro.
/// </summary>
public record StateResponse(long Id, string Name, string UF)
{
    /// <summary>
    /// Mapeia <see cref="State"/> para DTO.
    /// </summary>
    public static StateResponse FromDomain(State state) =>
        new(state.Id, state.Name, state.UF);
}
