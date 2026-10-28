using System.Text.Json.Serialization;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.DTOs;

/// <summary>
/// DTO de resposta para clínica, com a cidade aninhada.
/// </summary>
public record ClinicResponse(long Id, string Name, string Cnpj, string? Phone, CityResponse City)
{
    [JsonPropertyName("_links")]
    public IReadOnlyDictionary<string, LinkResponse> Links { get; init; }
        = new Dictionary<string, LinkResponse>();

    /// <summary>
    /// Mapeia <see cref="Clinic"/> para DTO.
    /// </summary>
    public static ClinicResponse FromDomain(Clinic clinic) =>
        new(clinic.Id, clinic.Name, clinic.Cnpj, clinic.Phone, CityResponse.FromDomain(clinic.City));
}
