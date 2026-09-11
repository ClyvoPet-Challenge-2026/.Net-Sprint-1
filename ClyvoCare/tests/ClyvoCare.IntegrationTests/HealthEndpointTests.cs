using System.Text.Json;

namespace ClyvoCare.IntegrationTests;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_DeveResponderComCorpoJsonDeChecks()
    {
        //Arrange

        //Act
        var response = await _client.GetAsync("/health");

        //Assert
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.True(json.RootElement.TryGetProperty("status", out _));
        Assert.True(json.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task GetMetrics_DeveResponderComTextoPrometheus()
    {
        //Arrange

        //Act
        var response = await _client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Contains("target_info", body);
    }
}
