using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Infrastructure.Persistence;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace ClyvoCare.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class TracingTests(CustomWebApplicationFactory factory)
{
    private async Task<Activity[]> CompletedTrace(string traceId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var spans = factory.Traces.Where(activity => activity.TraceId.ToString() == traceId).ToArray();
            if (spans.Any(activity => activity.Kind == ActivityKind.Server)) return spans;
            await Task.Delay(10);
        }
        throw new TimeoutException("The server trace was not completed.");
    }

    [Fact]
    public async Task Request_PreservesW3CParentAndLinksApplicationToPersistence()
    {
        using var client = factory.CreateClient();
        var traceId = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/clinicas?pageSize=1&nome=private-query-value");
        request.Headers.Add("traceparent", $"00-{traceId}-0123456789abcdef-01");
        request.Headers.Add("tracestate", "test=value");
        request.Headers.Add("Origin", "http://localhost:5173");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(traceId, Assert.Single(response.Headers.GetValues("X-Trace-Id")));
        Assert.Contains("X-Trace-Id", string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")));

        var spans = await CompletedTrace(traceId);
        var server = Assert.Single(spans, activity => activity.Kind == ActivityKind.Server);
        var application = Assert.Single(spans, activity => activity.DisplayName == "ClinicService.SearchPage");
        var repository = Assert.Single(spans, activity => activity.DisplayName == "ClinicRepository.SearchPage");
        Assert.Equal("0123456789abcdef", server.ParentSpanId.ToString());
        Assert.Equal("test=value", server.TraceStateString);
        Assert.Equal(server.SpanId, application.ParentSpanId);
        Assert.Equal(application.SpanId, repository.ParentSpanId);
        Assert.Equal(ActivityKind.Client, repository.Kind);
        Assert.All(spans, activity => Assert.DoesNotContain("private-query-value",
            string.Join(" ", activity.TagObjects.Select(tag => tag.Value))));
        Assert.Null(server.GetTagItem("url.query"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("00-00000000000000000000000000000000-0000000000000000-01")]
    public async Task Request_MissingOrInvalidParent_GeneratesFreshTrace(string? parent)
    {
        using var client = factory.CreateClient();
        if (parent is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("traceparent", parent);
        client.DefaultRequestHeaders.Add("X-Trace-Id", "untrusted-value");
        var response = await client.GetAsync("/api/estados");
        var traceId = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.NotEqual(new string('0', 32), traceId);
        var spans = await CompletedTrace(traceId);
        Assert.Contains(spans, activity => activity.DisplayName == "StateService.GetAll");
        Assert.Contains(spans, activity => activity.DisplayName == "Repository.State.GetAll");
    }

    [Fact]
    public async Task UnsampledParent_PreservesTraceWithoutForcingExport()
    {
        using var client = factory.CreateClient();
        var traceId = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("traceparent", $"00-{traceId}-0123456789abcdef-00");
        var response = await client.GetAsync("/api/cidades");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(traceId, Assert.Single(response.Headers.GetValues("X-Trace-Id")));
        Assert.DoesNotContain(factory.Traces, activity => activity.TraceId.ToString() == traceId);
    }

    [Theory]
    [InlineData("/api/clinicas?pageSize=101", HttpStatusCode.BadRequest)]
    [InlineData("/api/clinicas?cityId=invalid", HttpStatusCode.BadRequest)]
    [InlineData("/api/clinicas/9223372036854775807", HttpStatusCode.NotFound)]
    public async Task Errors_UseSameTraceInHeaderAndProblemDetails(string path, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var traceId = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("traceparent", $"00-{traceId}-0123456789abcdef-01");
        var response = await client.GetAsync(path);
        Assert.Equal(expected, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(traceId, json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(traceId, Assert.Single(response.Headers.GetValues("X-Trace-Id")));
    }

    [Fact]
    public void Service_Exception_DisposesSpanAndRestoresParent()
    {
        using var client = factory.CreateClient();
        using var parent = new Activity("service-parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        var repository = new Mock<ICityRepository>();
        repository.Setup(value => value.GetByIdWithState(1)).Callback(() =>
        {
            Assert.Equal("CityService.GetById", Activity.Current!.DisplayName);
            Assert.Equal(parent.TraceId, Activity.Current.TraceId);
        }).Throws(new InvalidOperationException("private-failure-value"));

        Assert.Throws<InvalidOperationException>(() => new CityService(repository.Object).GetById(1));
        Assert.Same(parent, Activity.Current);
        var span = Assert.Single(factory.Traces, activity => activity.TraceId == parent.TraceId);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
        Assert.Empty(span.Events);
        Assert.Empty(span.TagObjects);
    }

    [Fact]
    public void MongoFailure_MarksAuditSpanWithoutRecordingConnectionDetails()
    {
        using var client = factory.CreateClient();
        using var parent = new Activity("audit-parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        var collection = new Mock<IMongoCollection<BsonDocument>>();
        var database = new Mock<IMongoDatabase>();
        database.Setup(value => value.GetCollection<BsonDocument>("ClinicAuditLogs", null)).Returns(collection.Object);
        collection.Setup(value => value.InsertOne(It.IsAny<BsonDocument>(), null, default))
            .Throws(new TimeoutException("private-connection-value"));
        var repository = new ClinicAuditRepository(database.Object, NullLogger<ClinicAuditRepository>.Instance);

        Assert.False(repository.TryAdd(new ClinicAuditEntry(1, "CREATE", 1, DateTime.UtcNow, parent.TraceId.ToString())));
        Assert.Same(parent, Activity.Current);
        var span = Assert.Single(factory.Traces, activity => activity.TraceId == parent.TraceId);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("ClinicAuditRepository.TryAdd", span.DisplayName);
        Assert.Equal(typeof(TimeoutException).FullName, span.GetTagItem("error.type"));
        Assert.DoesNotContain("private-connection-value", string.Join(" ", span.TagObjects.Select(tag => tag.Value)));
        Assert.Empty(span.Events);
    }

    [Fact]
    public async Task IdentityLookup_AsyncSuccessAndCancellation_RestoreParent()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var repository = new ClinicalIdentityRepository(scope.ServiceProvider.GetRequiredService<ClyvoCareContext>());
        using var parent = new Activity("identity-parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        Assert.True(await repository.IsActiveOwnerAsync(1, "admin@example.com", "ADMIN", default));
        Assert.Same(parent, Activity.Current);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.IsActiveOwnerAsync(1, "admin@example.com", "ADMIN", cancellation.Token));
        Assert.Same(parent, Activity.Current);
        var spans = factory.Traces.Where(activity => activity.TraceId == parent.TraceId).ToArray();
        Assert.Equal(2, spans.Length);
        Assert.All(spans, activity =>
        {
            Assert.Equal("ClinicalIdentityRepository.IsActiveOwnerAsync", activity.DisplayName);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.Empty(activity.TagObjects);
        });
    }
}
