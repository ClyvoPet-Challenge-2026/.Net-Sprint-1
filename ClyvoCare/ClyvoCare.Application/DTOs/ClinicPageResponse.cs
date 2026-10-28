using System.Text.Json.Serialization;

namespace ClyvoCare.Application.DTOs;

public record ClinicPageResponse(IReadOnlyList<ClinicResponse> Items,
    int PageNumber, int PageSize, int TotalCount)
    : PagedResult<ClinicResponse>(Items, PageNumber, PageSize, TotalCount)
{
    [JsonPropertyName("_links")]
    public IReadOnlyDictionary<string, LinkResponse> Links { get; init; }
        = new Dictionary<string, LinkResponse>();
}
