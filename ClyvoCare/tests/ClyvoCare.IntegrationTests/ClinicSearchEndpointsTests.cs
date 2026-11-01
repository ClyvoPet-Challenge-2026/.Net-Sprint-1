using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class ClinicSearchEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly long[] _ids;

    public ClinicSearchEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
        var clinics = new[]
        {
            Clinic.Create("Query fixture A & Care", "91.111.111/0001-11", 1, null),
            Clinic.Create("Query fixture A & Care", "92.222.222/0001-22", 1, null),
            Clinic.Create("Query fixture B & Care", "93.333.333/0001-33", 1, null)
        };
        db.Clinics.AddRange(clinics);
        db.SaveChanges();
        _ids = clinics.Select(c => c.Id).ToArray();
    }

    private async Task<JsonDocument> Get(string path, HttpClient? client = null) =>
        JsonDocument.Parse(await (client ?? _client).GetStringAsync(path));

    [Fact]
    public async Task Get_NoParameters_PreservesArrayAndPublicResourceLinks()
    {
        using var result = await Get("/api/clinicas");
        Assert.Equal(JsonValueKind.Array, result.RootElement.ValueKind);
        var clinic = result.RootElement.EnumerateArray().Single(c => c.GetProperty("id").GetInt64() == _ids[0]);
        var links = clinic.GetProperty("_links");
        Assert.False(links.TryGetProperty("update", out _));
        Assert.False(links.TryGetProperty("delete", out _));
        Assert.Equal("GET", links.GetProperty("self").GetProperty("method").GetString());
        using var detail = await Get(links.GetProperty("self").GetProperty("href").GetString()!);
        Assert.Equal(_ids[0], detail.RootElement.GetProperty("id").GetInt64());
        using var city = await Get(links.GetProperty("by-city").GetProperty("href").GetString()!);
        Assert.Contains(city.RootElement.EnumerateArray(), c => c.GetProperty("id").GetInt64() == _ids[0]);
    }

    [Fact]
    public async Task Get_FilteredPages_FollowLinksAndPreserveEncodedFilters()
    {
        var path = "/api/clinicas?nome=" + Uri.EscapeDataString("Query fixture A & Care") + "&pageSize=1&sortBy=name";
        using var first = await Get(path);
        var page = first.RootElement;
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(_ids[0], page.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.False(page.GetProperty("_links").TryGetProperty("prev", out _));
        string next = page.GetProperty("_links").GetProperty("next").GetProperty("href").GetString()!;
        Assert.StartsWith("/api/clinicas?", next);
        Assert.Contains("%26", next);
        using var second = await Get(next);
        Assert.Equal(2, second.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(_ids[1], second.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.False(second.RootElement.GetProperty("_links").TryGetProperty("next", out _));
        using var previous = await Get(second.RootElement.GetProperty("_links").GetProperty("prev").GetProperty("href").GetString()!);
        Assert.Equal(_ids[0], previous.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Get_CombinedFiltersAndEmptyPages_Return200WithCounts()
    {
        using var filtered = await Get("/api/clinicas?nome=fixture&cnpj=92.222.222%2F0001-22&cityId=1");
        Assert.Equal(_ids[1], filtered.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(1, filtered.RootElement.GetProperty("totalCount").GetInt32());
        using var outside = await Get("/api/clinicas?nome=Query%20fixture&pageNumber=99&pageSize=1");
        Assert.Equal(0, outside.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(3, outside.RootElement.GetProperty("totalCount").GetInt32());
        using var empty = await Get("/api/clinicas?cityId=999999");
        Assert.Equal(0, empty.RootElement.GetProperty("totalCount").GetInt32());
        Assert.False(empty.RootElement.GetProperty("_links").TryGetProperty("next", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/clinicas/por-cidade/999999")).StatusCode);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageNumber=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    [InlineData("pageNumber=invalid")]
    [InlineData("cityId=0")]
    [InlineData("sortBy=unknown")]
    [InlineData("sortDirection=invalid")]
    [InlineData("cnpj=invalid")]
    public async Task Get_InvalidSearch_Returns400(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/clinicas?" + query)).StatusCode);
    }

    [Fact]
    public async Task Get_MutationLinks_AreOnlyAdvertisedToAdmin()
    {
        using var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(new JwtPayload
        {
            ["iss"] = "clyvocare-api", ["sub"] = "owner@example.com", ["ownerId"] = 2L, ["role"] = "OWNER",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        }));
        using var ownResult = await Get($"/api/clinicas/{_ids[0]}", owner);
        Assert.False(ownResult.RootElement.GetProperty("_links").TryGetProperty("update", out _));
        using var admin = _factory.CreateAdminClient();
        using var result = await Get($"/api/clinicas/{_ids[0]}", admin);
        var links = result.RootElement.GetProperty("_links");
        Assert.Equal("PUT", links.GetProperty("update").GetProperty("method").GetString());
        Assert.Equal("DELETE", links.GetProperty("delete").GetProperty("method").GetString());
        using var legacy = await Get("/api/clinicas/buscar?nome=Query%20fixture", admin);
        Assert.Equal(JsonValueKind.Array, legacy.RootElement.ValueKind);
        Assert.Equal(3, legacy.RootElement.GetArrayLength());
        Assert.True(legacy.RootElement[0].GetProperty("_links").TryGetProperty("update", out _));
    }

    [Fact]
    public async Task Swagger_ClinicList_DescribesBothArrayAndPage()
    {
        using var document = await Get("/swagger/v1/swagger.json");
        var schema = document.RootElement.GetProperty("paths").GetProperty("/api/clinicas").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal(2, schema.GetProperty("oneOf").GetArrayLength());
        Assert.Equal("array", schema.GetProperty("oneOf")[0].GetProperty("type").GetString());
        Assert.EndsWith("/ClinicPageResponse", schema.GetProperty("oneOf")[1].GetProperty("$ref").GetString());
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ClyvoCareContext>().Clinics.Where(c => _ids.Contains(c.Id)).ExecuteDelete();
        _client.Dispose();
    }
}
