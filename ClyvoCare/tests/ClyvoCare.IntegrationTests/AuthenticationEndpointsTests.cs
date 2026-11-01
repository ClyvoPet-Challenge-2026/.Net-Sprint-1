using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AuthenticationEndpointsTests(CustomWebApplicationFactory factory)
{
    private static JwtPayload AdminPayload() => new()
    {
        ["iss"] = "clyvocare-api", ["sub"] = "admin@example.com", ["ownerId"] = 1L, ["role"] = "ADMIN",
        ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
    };

    private static ClinicRequest Request() => new("Auth test clinic", "77.777.777/0001-77", 1, null);

    [Theory]
    [InlineData("/api/clinicas")]
    [InlineData("/api/clinicas/buscar?nome=Vet")]
    [InlineData("/api/clinicas/por-cidade/1")]
    [InlineData("/api/cidades")]
    [InlineData("/api/estados")]
    public async Task Get_PublicConsultations_DoesNotRequireToken(string path)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ClinicWrite_VisitorOrOwner_IsRejectedWithoutMutation(string method)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
        var before = await db.Clinics.CountAsync();
        using var client = factory.CreateClient();
        string path = method == "POST" ? "/api/clinicas" : "/api/clinicas/999999";
        using var anonymous = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(Request()) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(anonymous)).StatusCode);

        var payload = AdminPayload();
        payload["ownerId"] = 2L;
        payload["sub"] = "owner@example.com";
        payload["role"] = "OWNER";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(payload));
        using var owner = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(Request()) };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(owner)).StatusCode);
        Assert.Equal(before, await db.Clinics.CountAsync());
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("noExpiry")]
    [InlineData("notYetValid")]
    [InlineData("wrongIssuer")]
    [InlineData("missingOwner")]
    [InlineData("invalidOwner")]
    [InlineData("zeroOwner")]
    [InlineData("negativeOwner")]
    [InlineData("overflowOwner")]
    [InlineData("multipleOwners")]
    [InlineData("missingSubject")]
    [InlineData("unknownRole")]
    [InlineData("multipleRoles")]
    [InlineData("changedRole")]
    [InlineData("wrongEmail")]
    [InlineData("unknownAccount")]
    [InlineData("disabledOwner")]
    [InlineData("disabledAdmin")]
    public async Task ClinicWrite_InvalidOrStaleIdentity_Returns401(string scenario)
    {
        var payload = AdminPayload();
        switch (scenario)
        {
            case "expired": payload["exp"] = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(); break;
            case "noExpiry": payload.Remove("exp"); break;
            case "notYetValid": payload["nbf"] = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(); break;
            case "wrongIssuer": payload["iss"] = "other-api"; break;
            case "missingOwner": payload.Remove("ownerId"); break;
            case "invalidOwner": payload["ownerId"] = "abc"; break;
            case "zeroOwner": payload["ownerId"] = 0; break;
            case "negativeOwner": payload["ownerId"] = -1; break;
            case "overflowOwner": payload["ownerId"] = "9223372036854775808"; break;
            case "multipleOwners": payload["ownerId"] = new[] { 1, 2 }; break;
            case "missingSubject": payload.Remove("sub"); break;
            case "unknownRole": payload["role"] = "GUEST"; break;
            case "multipleRoles": payload["role"] = new[] { "ADMIN", "OWNER" }; break;
            case "changedRole": payload["role"] = "OWNER"; break;
            case "wrongEmail": payload["sub"] = "owner@example.com"; break;
            case "unknownAccount": payload["ownerId"] = 999L; break;
            case "disabledOwner": payload["ownerId"] = 3L; payload["sub"] = "disabled@example.com"; payload["role"] = "OWNER"; break;
            case "disabledAdmin": payload["ownerId"] = 4L; payload["sub"] = "disabled-admin@example.com"; break;
            default: throw new ArgumentException("Unknown scenario", nameof(scenario));
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(payload));

        var response = await client.PostAsJsonAsync("/api/clinicas", Request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Null(response.Headers.WwwAuthenticate.Single().Parameter);
    }

    [Theory]
    [InlineData("unsigned")]
    [InlineData("wrongAlgorithm")]
    [InlineData("modifiedSignature")]
    public async Task ClinicWrite_InvalidSignature_Returns401(string scenario)
    {
        var payload = AdminPayload();
        string token;
        if (scenario == "unsigned")
            token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                new JwtHeader { ["alg"] = "none", ["typ"] = "JWT" }, payload));
        else if (scenario == "wrongAlgorithm")
            token = factory.CreateToken(payload, SecurityAlgorithms.RsaSha512);
        else
        {
            token = factory.CreateToken(payload);
            int index = token.LastIndexOf('.') + 5;
            token = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/clinicas", Request())).StatusCode);
    }

    [Fact]
    public async Task ExistingToken_AccountIsDeactivated_IsRejectedOnTheNextRequest()
    {
        var payload = AdminPayload();
        payload["ownerId"] = 5L;
        payload["sub"] = "deactivate@example.com";
        payload["role"] = "OWNER";
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(payload));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/clinicas", Request())).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
        await db.Database.ExecuteSqlRawAsync("UPDATE TB_CAD_OWNER SET ENABLED = 'N' WHERE ID = 5");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/clinicas", Request())).StatusCode);
    }

    [Fact]
    public async Task Swagger_ProtectedOperations_DeclareBearerAndErrorsOnlyForWrites()
    {
        using var client = factory.CreateClient();
        using var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var document = swagger.RootElement;
        Assert.Equal("bearer", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = document.GetProperty("paths");
        var reads = paths.GetProperty("/api/clinicas").GetProperty("get");
        Assert.False(reads.TryGetProperty("security", out var readSecurity) && readSecurity.GetArrayLength() > 0);
        var writes = paths.GetProperty("/api/clinicas").GetProperty("post");
        Assert.True(writes.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(writes.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(writes.GetProperty("responses").TryGetProperty("403", out _));
    }
}
