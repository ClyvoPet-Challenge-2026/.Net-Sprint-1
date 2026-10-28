using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClyvoCare.Application.DTOs;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClinicSearchEndpointsTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Swagger_DeveDocumentarListaEPagina()
    {
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var alternatives = json.RootElement.GetProperty("paths").GetProperty("/api/clinicas")
            .GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("oneOf");
        Assert.Equal(2, alternatives.GetArrayLength());
        Assert.Equal("array", alternatives[0].GetProperty("type").GetString());
        Assert.EndsWith("/ClinicPageResponse", alternatives[1].GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task SearchPage_DeveFiltrarOrdenarEDesempatarPorId_PreservandoLinks()
    {
        using var admin = factory.CreateAuthenticatedClient();
        using var client = factory.CreateClient();
        var prefix = $"Page & {Guid.NewGuid():N}";
        var firstB = await Create(admin, prefix + " B", "91.000.000/0001-01");
        var a = await Create(admin, prefix + " A", "91.000.000/0001-02");
        var secondB = await Create(admin, prefix + " B", "91.000.000/0001-03");

        var page = await client.GetFromJsonAsync<ClinicPageResponse>(
            $"/api/clinicas?pageSize=1&sortBy=name&sortDirection=desc&cityId=1&nome={Uri.EscapeDataString(prefix)}");

        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(firstB.Id, Assert.Single(page.Items).Id);
        Assert.False(page.Links.ContainsKey("prev"));
        Assert.False(page.Items[0].Links.ContainsKey("update"));
        var next = await client.GetFromJsonAsync<ClinicPageResponse>(page.Links["next"].Href);
        Assert.Equal(secondB.Id, Assert.Single(next!.Items).Id);
        Assert.Equal(3, next.TotalCount);
        var last = await client.GetFromJsonAsync<ClinicPageResponse>(page.Links["last"].Href);
        Assert.Equal(a.Id, Assert.Single(last!.Items).Id);
        Assert.False(last.Links.ContainsKey("next"));
        Assert.Equal(2, (await client.GetFromJsonAsync<ClinicPageResponse>(last.Links["prev"].Href))!.PageNumber);

        var exact = await client.GetFromJsonAsync<ClinicPageResponse>(
            $"/api/clinicas?nome={Uri.EscapeDataString(prefix)}&cnpj={Uri.EscapeDataString(a.Cnpj)}&cityId=1");
        Assert.Equal(a.Id, Assert.Single(exact!.Items).Id);
        Assert.Equal(1, exact.TotalCount);
        var pastEnd = await client.GetFromJsonAsync<ClinicPageResponse>(
            $"/api/clinicas?pageNumber=10&pageSize=1&nome={Uri.EscapeDataString(prefix)}");
        Assert.Empty(pastEnd!.Items);
        Assert.Equal(3, pastEnd.TotalCount);
        Assert.False(pastEnd.Links.ContainsKey("next"));
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    [InlineData("sortBy=anything")]
    [InlineData("sortDirection=anything")]
    [InlineData("cityId=-1")]
    [InlineData("pageSize=text")]
    public async Task SearchPage_ComParametrosInvalidos_DeveRetornar400(string query)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/clinicas?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchPage_SemResultados_DeveRetornarPaginaVaziaSemNextOuPrev()
    {
        using var client = factory.CreateClient();
        var page = await client.GetFromJsonAsync<ClinicPageResponse>("/api/clinicas?cityId=999999");
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.Links.ContainsKey("next"));
        Assert.False(page.Links.ContainsKey("prev"));
    }

    [Fact]
    public async Task ConsultasLegadas_DevemManterArray_ELinksDevemRespeitarPapel()
    {
        using var client = factory.CreateClient();
        using var admin = factory.CreateAuthenticatedClient();
        using var owner = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        foreach (var path in new[] { "/api/clinicas", "/api/clinicas/por-cidade/1", "/api/clinicas/buscar?nome=Clinica" })
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(path));
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        }
        var clinic = await client.GetFromJsonAsync<ClinicResponse>("/api/clinicas/100");
        Assert.Equal("GET", clinic!.Links["self"].Method);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(clinic.Links["by-city"].Href)).StatusCode);
        Assert.False(clinic.Links.ContainsKey("delete"));
        Assert.False((await owner.GetFromJsonAsync<ClinicResponse>(clinic.Links["self"].Href))!.Links.ContainsKey("update"));
        var privileged = await admin.GetFromJsonAsync<ClinicResponse>(clinic.Links["self"].Href);
        Assert.Equal("PUT", privileged!.Links["update"].Method);
        Assert.Equal("DELETE", privileged.Links["delete"].Method);
    }

    private static async Task<ClinicResponse> Create(HttpClient client, string name, string cnpj)
    {
        var response = await client.PostAsJsonAsync("/api/clinicas", new ClinicRequest(name, cnpj, 1, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClinicResponse>())!;
    }
}
