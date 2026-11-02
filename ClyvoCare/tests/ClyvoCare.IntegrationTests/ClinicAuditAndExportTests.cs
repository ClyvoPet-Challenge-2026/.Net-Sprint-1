using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClyvoCare.Application.DTOs;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClinicAuditAndExportTests(CustomWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Export_RequiresAdmin(bool owner, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        if (owner)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                factory.CreateToken(new JwtPayload
                {
                    ["iss"] = "clyvocare-api", ["sub"] = "owner@example.com", ["ownerId"] = 2L,
                    ["role"] = "OWNER", ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
                }));
        Assert.Equal(expected, (await client.GetAsync("/api/clinicas/exportar")).StatusCode);
    }

    [Fact]
    public async Task Export_DownloadsRelationalDatasetWithEmbeddedLocationAndNoLinks()
    {
        using var client = factory.CreateAdminClient();
        var relational = (await client.GetFromJsonAsync<ClinicResponse[]>("/api/clinicas"))!;
        var response = await client.GetAsync("/api/clinicas/exportar");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("clinics.json", response.Content.Headers.ContentDisposition!.FileNameStar);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var documents = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(relational.Length, documents.Length);
        foreach (var document in documents)
        {
            var source = Assert.Single(relational, clinic => clinic.Id == document.GetProperty("_id").GetInt64());
            Assert.Equal(source.Name, document.GetProperty("name").GetString());
            Assert.Equal(source.City.Id, document.GetProperty("city").GetProperty("id").GetInt64());
            Assert.Equal(source.City.State.UF, document.GetProperty("city").GetProperty("state").GetProperty("uf").GetString());
            Assert.False(document.TryGetProperty("_links", out _));
            Assert.False(document.TryGetProperty("id", out _));
        }
    }

    [Fact]
    public async Task Mutations_AuditSuccessfulWritesWithActorAndTrace()
    {
        using var client = factory.CreateAdminClient();
        using var activity = new Activity("audit-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        client.DefaultRequestHeaders.Add("traceparent", activity.Id);
        var request = new ClinicRequest("Audit clinic", "60.111.111/0001-11", 1, null);
        var created = await client.PostAsJsonAsync("/api/clinicas", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var clinic = (await created.Content.ReadFromJsonAsync<ClinicResponse>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/clinicas/{clinic.Id}",
            request with { Name = "Updated audit clinic" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/clinicas/{clinic.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/clinicas/{clinic.Id}")).StatusCode);
        var entries = factory.Audit.Entries.Where(entry => entry.TraceId == activity.TraceId.ToString()).ToArray();
        Assert.Equal(new[] { "CREATE", "UPDATE", "DELETE" }, entries.Select(entry => entry.Operation));
        Assert.All(entries, entry =>
        {
            Assert.Equal(clinic.Id, entry.ClinicId);
            Assert.Equal(1, entry.ActorId);
            Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
            Assert.Equal(activity.TraceId.ToString(), entry.TraceId);
        });
    }

    [Fact]
    public async Task MongoOutage_PreservesRelationalCrudAndExport()
    {
        using var client = factory.CreateAdminClient();
        var previousCount = factory.Audit.Entries.Count;
        factory.Audit.Available = false;
        try
        {
            var created = await client.PostAsJsonAsync("/api/clinicas",
                new ClinicRequest("Outage clinic", "61.111.111/0001-11", 1, null));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var clinic = (await created.Content.ReadFromJsonAsync<ClinicResponse>())!;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/clinicas/{clinic.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/clinicas/exportar")).StatusCode);
            Assert.Equal(previousCount, factory.Audit.Entries.Count);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/clinicas/{clinic.Id}")).StatusCode);
        }
        finally { factory.Audit.Available = true; }
    }
}
