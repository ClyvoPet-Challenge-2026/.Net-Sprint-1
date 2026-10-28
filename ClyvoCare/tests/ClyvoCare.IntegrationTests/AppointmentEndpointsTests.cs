using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClyvoCare.Application.DTOs;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AppointmentEndpointsTests(CustomWebApplicationFactory factory)
{
    private static long _slot;

    private static AppointmentRequest ValidRequest(long petId = 1000) => new(petId, 100, "CONSULTATION",
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(10), TimeSpan.Zero)
            .AddMinutes(Interlocked.Increment(ref _slot)), "Check-up Geral", "Consulta de rotina");

    [Fact]
    public async Task Post_ComTutorAutenticado_DevePersistirERetornarLocation()
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        var request = ValidRequest();
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", request);
        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
        using var secondClient = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        var persisted = await secondClient.GetFromJsonAsync<AppointmentResponse>(response.Headers.Location);
        Assert.NotNull(created);
        Assert.Equal(created, persisted);
        Assert.Equal("SCHEDULED", persisted!.Status);
        Assert.Equal(request.Reason, persisted.Reason);
        Assert.Equal(request.Notes, persisted.Notes);
        Assert.Equal(TimeSpan.Zero, persisted.ScheduledAt.Offset);
    }

    [Fact]
    public async Task Post_SemToken_DeveRetornar401()
    {
        //Arrange
        using var client = factory.CreateClient();
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest());
        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ComPetDeOutroTutor_DeveRetornar403()
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest(2000));
        //Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ComOutroTutor_DeveOcultarRegistroEFiltrarLista()
    {
        //Arrange
        using var other = factory.CreateAuthenticatedClient(300, "other@test.com", "OWNER");
        var createdResponse = await other.PostAsJsonAsync("/api/agendamentos", ValidRequest(2000));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        using var owner = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        //Act
        var response = await owner.GetAsync(createdResponse.Headers.Location);
        var page = await owner.GetFromJsonAsync<PagedResult<AppointmentResponse>>("/api/agendamentos?petId=2000&pageSize=1");
        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Post_ComAdministrador_DevePermitirPetDeOutroTutor()
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient();
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest(2000));
        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(999999, 100)]
    [InlineData(1000, 999999)]
    public async Task Post_ComReferenciaInexistente_DeveRetornar404(long petId, long clinicId)
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest(petId) with { ClinicId = clinicId });
        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ComDataPassadaOuTipoInvalido_DeveRetornar400()
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        //Act
        var past = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest() with { ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1) });
        var invalidType = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest() with { EventType = "checkup" });
        var missingDate = await client.PostAsJsonAsync("/api/agendamentos", ValidRequest() with { ScheduledAt = null });
        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingDate.StatusCode);
    }

    [Fact]
    public async Task Post_ComHorarioSemFuso_DeveRetornar400()
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        var body = new { petId = 1000, clinicId = 100, eventType = "EXAM", reason = "Exame", scheduledAt = "2030-10-27T09:00:00" };
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", body);
        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Duplicado_DeveRetornar409()
    {
        //Arrange
        using var client = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        var request = ValidRequest();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/agendamentos", request)).StatusCode);
        //Act
        var response = await client.PostAsJsonAsync("/api/agendamentos", request);
        //Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("identity")]
    [InlineData("missing-id")]
    public async Task Get_ComTokenInvalido_DeveRetornar401(string scenario)
    {
        //Arrange
        using var client = factory.CreateClient();
        var token = factory.CreateToken(200, scenario == "identity" ? "other@test.com" : "owner@test.com", "OWNER",
            lifetime: scenario == "expired" ? TimeSpan.FromMinutes(-5) : null,
            issuer: scenario == "issuer" ? "invalid-issuer" : "clyvocare-api",
            invalidSignature: scenario == "signature", includeOwnerId: scenario != "missing-id");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        //Act
        var response = await client.GetAsync("/api/agendamentos");
        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Clinicas_Escrita_DeveExigirAdministrador()
    {
        //Arrange
        using var anonymous = factory.CreateClient();
        using var owner = factory.CreateAuthenticatedClient(200, "owner@test.com", "OWNER");
        var request = new ClinicRequest("Clinica", "98.765.432/0001-00", 1, null);
        //Act
        var noToken = await anonymous.PostAsJsonAsync("/api/clinicas", request);
        var notAdmin = await owner.PostAsJsonAsync("/api/clinicas", request);
        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notAdmin.StatusCode);
    }
}
