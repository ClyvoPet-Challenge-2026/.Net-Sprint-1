using System.Text.Json.Serialization;

namespace ClyvoCare.Application.DTOs;

public sealed record ClinicExportResponse([property: JsonPropertyName("_id")] long Id,
    string Name, string Cnpj, string? Phone, CityResponse City);
