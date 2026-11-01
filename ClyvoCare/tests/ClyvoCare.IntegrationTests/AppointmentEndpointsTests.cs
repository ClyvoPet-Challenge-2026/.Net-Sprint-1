using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AppointmentEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _owner;
    private readonly HttpClient _admin;
    private readonly long _clinicId;

    public AppointmentEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _owner = OwnerClient(2, "owner@example.com");
        _admin = factory.CreateAdminClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
        db.Appointments.ExecuteDelete();
        _clinicId = db.Clinics.Single(c => c.Cnpj == "88.888.888/0001-88").Id;
    }

    private HttpClient OwnerClient(long id, string email)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(new JwtPayload
        {
            ["iss"] = "clyvocare-api", ["sub"] = email, ["ownerId"] = id, ["role"] = "OWNER",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        }));
        return client;
    }

    private AppointmentRequest Request() => new(1, _clinicId, "CONSULTATION",
        DateTimeOffset.UtcNow.AddDays(10), " Check-up ", " Notes ");

    [Fact]
    public async Task Post_ValidBooking_Returns201LocationAndPersistedDetails()
    {
        var request = Request();
        var response = await _owner.PostAsJsonAsync("/api/agendamentos", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AppointmentResponse>())!;
        Assert.True(created.Id > 0);
        Assert.Equal($"/api/agendamentos/{created.Id}", new Uri(_owner.BaseAddress!, response.Headers.Location!).AbsolutePath);
        Assert.Equal("Check-up", created.Reason);
        Assert.Equal("Notes", created.Notes);
        Assert.Equal("SCHEDULED", created.Status);
        Assert.Equal(TimeSpan.Zero, created.ScheduledAt.Offset);

        var saved = await _owner.GetFromJsonAsync<AppointmentResponse>(response.Headers.Location);
        Assert.Equal(created, saved);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
        var row = db.Appointments.Single(a => a.Id == created.Id);
        Assert.Equal("Check-up", row.Reason);
        Assert.Equal("Notes", row.Notes);
        Assert.Equal(0, row.EventDate.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Theory]
    [InlineData("past")]
    [InlineData("missingReason")]
    [InlineData("longReason")]
    [InlineData("longNotes")]
    [InlineData("invalidType")]
    [InlineData("invalidPet")]
    public async Task Post_InvalidRequest_Returns400WithoutInsertion(string scenario)
    {
        var request = Request();
        request = scenario switch
        {
            "past" => request with { ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1) },
            "missingReason" => request with { Reason = " " },
            "longReason" => request with { Reason = new string('r', 151) },
            "longNotes" => request with { Notes = new string('n', 1001) },
            "invalidType" => request with { EventType = "UNKNOWN" },
            "invalidPet" => request with { PetId = 0 },
            _ => throw new ArgumentException(scenario)
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.PostAsJsonAsync("/api/agendamentos", request)).StatusCode);
        Assert.Equal(0, AppointmentCount());
    }

    [Theory]
    [InlineData("\"2035-01-01T12:00:00\"")]
    [InlineData("\"2035-01-01\"")]
    [InlineData("123")]
    [InlineData("null")]
    [InlineData("\"invalid\"")]
    public async Task Post_InvalidDateRepresentation_Returns400(string dateJson)
    {
        string body = $$"""
            {"petId":1,"clinicId":{{_clinicId}},"eventType":"CONSULTATION","scheduledAt":{{dateJson}},"reason":"Reason"}
            """;
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.PostAsync("/api/agendamentos", content)).StatusCode);
        Assert.Equal(0, AppointmentCount());
    }

    [Fact]
    public async Task Post_EquivalentOffsetAndPrecision_Returns409ForDuplicate()
    {
        var request = Request();
        var first = await _owner.PostAsJsonAsync("/api/agendamentos", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = (await first.Content.ReadFromJsonAsync<AppointmentResponse>())!;
        var duplicate = request with { ScheduledAt = created.ScheduledAt.ToOffset(TimeSpan.FromHours(-3)).AddTicks(1) };
        Assert.Equal(HttpStatusCode.Conflict, (await _owner.PostAsJsonAsync("/api/agendamentos", duplicate)).StatusCode);
        Assert.Equal(1, AppointmentCount());
    }

    [Fact]
    public async Task Post_OtherOwnersPet_IsForbiddenAndHiddenOnRead()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _owner.PostAsJsonAsync("/api/agendamentos", Request() with { PetId = 2 })).StatusCode);
        Assert.Equal(0, AppointmentCount());
        var created = await _admin.PostAsJsonAsync("/api/agendamentos", Request() with { PetId = 2 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _owner.GetAsync(created.Headers.Location)).StatusCode);
        var list = await _owner.GetFromJsonAsync<PagedResult<AppointmentResponse>>("/api/agendamentos");
        Assert.Empty(list!.Items);
        var filtered = await _owner.GetFromJsonAsync<PagedResult<AppointmentResponse>>("/api/agendamentos?petId=2");
        Assert.Empty(filtered!.Items);
        using var other = OwnerClient(6, "other@example.com");
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync(created.Headers.Location)).StatusCode);
    }

    [Theory]
    [InlineData("pet")]
    [InlineData("clinic")]
    public async Task Post_MissingReference_Returns404(string reference)
    {
        var request = reference == "pet" ? Request() with { PetId = 999999 } : Request() with { ClinicId = 999999 };
        Assert.Equal(HttpStatusCode.NotFound, (await _owner.PostAsJsonAsync("/api/agendamentos", request)).StatusCode);
        Assert.Equal(0, AppointmentCount());
    }

    [Fact]
    public async Task Get_FilteredPages_KeepOwnerScopeCountsAndStableOrder()
    {
        var request = Request();
        var first = await _owner.PostAsJsonAsync("/api/agendamentos", request);
        var firstId = (await first.Content.ReadFromJsonAsync<AppointmentResponse>())!.Id;
        Assert.Equal(HttpStatusCode.Created, (await _owner.PostAsJsonAsync("/api/agendamentos", request with { ScheduledAt = request.ScheduledAt.AddHours(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _admin.PostAsJsonAsync("/api/agendamentos", request with { PetId = 2 })).StatusCode);
        var page = (await _owner.GetFromJsonAsync<PagedResult<AppointmentResponse>>("/api/agendamentos?petId=1&pageSize=1"))!;
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(firstId, Assert.Single(page.Items).Id);
        var empty = (await _owner.GetFromJsonAsync<PagedResult<AppointmentResponse>>("/api/agendamentos?pageNumber=3&pageSize=1"))!;
        Assert.Empty(empty.Items);
        Assert.Equal(2, empty.TotalCount);
        var all = (await _admin.GetFromJsonAsync<PagedResult<AppointmentResponse>>("/api/agendamentos"))!;
        Assert.Equal(3, all.TotalCount);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    [InlineData("petId=0")]
    public async Task Get_InvalidPagination_Returns400(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.GetAsync("/api/agendamentos?" + query)).StatusCode);
    }

    [Fact]
    public async Task Requests_AnonymousOrUnknownEvent_ReturnExpectedErrors()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/agendamentos", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/agendamentos")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _owner.GetAsync("/api/agendamentos/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.GetAsync("/api/agendamentos/0")).StatusCode);
    }

    private int AppointmentCount()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ClyvoCareContext>().Appointments.Count();
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ClyvoCareContext>().Appointments.ExecuteDelete();
        _owner.Dispose();
        _admin.Dispose();
    }
}
