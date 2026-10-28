using System.Text.Json;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class HealthEndpointTests
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ComDependenciasSubstituidas_DeveResponderComContratoJsonDeChecks()
    {
        //Arrange

        //Act
        var response = await _client.GetAsync("/health");

        //Assert
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.True(json.RootElement.TryGetProperty("status", out _));
        Assert.True(json.RootElement.TryGetProperty("checks", out var checks));
        var names = checks.EnumerateArray().Select(check => check.GetProperty("name").GetString()).ToArray();
        Assert.Contains("Oracle FIAP", names);
        Assert.Contains("FIAP", names);
        Assert.Contains("Google", names);
        Assert.Contains("MongoDB Audit", names);
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
