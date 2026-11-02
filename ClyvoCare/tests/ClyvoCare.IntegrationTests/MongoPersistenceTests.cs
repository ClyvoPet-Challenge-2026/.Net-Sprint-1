using ClyvoCare.API.Extensions;
using ClyvoCare.API.Health;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace ClyvoCare.IntegrationTests;

public sealed class MongoPersistenceTests
{
    private static ClinicAuditEntry Entry() => new(10, "CREATE", 1,
        new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), "0123456789abcdef0123456789abcdef");

    [Fact]
    public void Audit_WritesExpectedBsonTypesAndMetadata()
    {
        var collection = new Mock<IMongoCollection<BsonDocument>>();
        var database = new Mock<IMongoDatabase>();
        database.Setup(db => db.GetCollection<BsonDocument>("ClinicAuditLogs", null)).Returns(collection.Object);
        BsonDocument? document = null;
        collection.Setup(value => value.InsertOne(It.IsAny<BsonDocument>(), null, default))
            .Callback<BsonDocument, InsertOneOptions, CancellationToken>((value, _, _) => document = value);
        var repository = new ClinicAuditRepository(database.Object, NullLogger<ClinicAuditRepository>.Instance);

        Assert.True(repository.TryAdd(Entry()));
        Assert.NotNull(document);
        Assert.Equal(10, document["clinicId"].AsInt64);
        Assert.Equal(1, document["actorId"].AsInt64);
        Assert.Equal("CREATE", document["operation"].AsString);
        Assert.Equal(Entry().OccurredAtUtc, document["occurredAtUtc"].ToUniversalTime());
        Assert.Equal(Entry().TraceId, document["traceId"].AsString);
        Assert.Equal(5, document.ElementCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Audit_DriverFailure_IsBestEffortAndLogsNoConnectionDetails(bool timeout)
    {
        var collection = new Mock<IMongoCollection<BsonDocument>>();
        var database = new Mock<IMongoDatabase>();
        database.Setup(db => db.GetCollection<BsonDocument>("ClinicAuditLogs", null)).Returns(collection.Object);
        collection.Setup(value => value.InsertOne(It.IsAny<BsonDocument>(), null, default))
            .Throws(timeout ? new TimeoutException("secret connection detail") : new MongoException("secret connection detail"));
        var logger = new Mock<ILogger<ClinicAuditRepository>>();
        var repository = new ClinicAuditRepository(database.Object, logger.Object);

        Assert.False(repository.TryAdd(Entry()));
        logger.Verify(value => value.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Oracle changes remain committed")
                && !state.ToString()!.Contains("secret")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Health_PingsMongoAndDegradesWithoutExposingErrors(bool available)
    {
        var database = new Mock<IMongoDatabase>();
        var command = database.Setup(db => db.RunCommandAsync<BsonDocument>(
            It.IsAny<Command<BsonDocument>>(), null, default));
        if (available) command.ReturnsAsync(new BsonDocument("ok", 1));
        else command.ThrowsAsync(new MongoException("secret connection detail"));

        var result = await new MongoHealthCheck(database.Object).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(available ? HealthStatus.Healthy : HealthStatus.Degraded, result.Status);
        Assert.Null(result.Exception);
        Assert.DoesNotContain("secret", result.Description ?? "");
        database.Verify(db => db.RunCommandAsync<BsonDocument>(It.IsAny<Command<BsonDocument>>(), null, default), Times.Once);
    }

    [Theory]
    [InlineData(null, "clyvocare")]
    [InlineData("mongodb://localhost:27017", " ")]
    public void Configuration_MissingValues_FailsValidation(string? uri, string? name)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = uri, ["Mongo:DatabaseName"] = name
        }).Build();
        var services = new ServiceCollection().AddLogging().AddClyvoCareMongo(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MongoOptions>>().Value);
    }

    [Fact]
    public void Configuration_ReusesClientAcrossScopesWithBoundedTimeouts()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = "mongodb://localhost:27017", ["Mongo:DatabaseName"] = "clyvocare"
        }).Build();
        using var provider = new ServiceCollection().AddLogging().AddClyvoCareMongo(configuration).BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var client = first.ServiceProvider.GetRequiredService<IMongoClient>();
        Assert.Same(client, second.ServiceProvider.GetRequiredService<IMongoClient>());
        Assert.Equal(TimeSpan.FromSeconds(3), client.Settings.ServerSelectionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), client.Settings.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), client.Settings.SocketTimeout);
        Assert.Equal("clyvocare", provider.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName);
    }
}
