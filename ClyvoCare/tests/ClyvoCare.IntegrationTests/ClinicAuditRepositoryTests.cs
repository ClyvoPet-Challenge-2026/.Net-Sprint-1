using ClyvoCare.Application.DTOs;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace ClyvoCare.IntegrationTests;

// Testa o adaptador com doubles do driver; não exige nem valida um servidor MongoDB.
public sealed class ClinicAuditRepositoryTests
{
    private readonly Mock<IMongoCollection<BsonDocument>> _collection = new();
    private readonly ClinicAuditRepository _repository;

    public ClinicAuditRepositoryTests()
    {
        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.GetCollection<BsonDocument>(ClinicAuditRepository.CollectionName, null))
            .Returns(_collection.Object);
        _repository = new ClinicAuditRepository(database.Object, NullLogger<ClinicAuditRepository>.Instance);
    }

    private static ClinicAuditEntry Entry() => new("audit-test", 10, "Created", 100,
        new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc), null);

    [Fact]
    public void TryAdd_DevePersistirMetadadosComDataBsonUtc()
    {
        BsonDocument? saved = null;
        _collection.Setup(c => c.InsertOne(It.IsAny<BsonDocument>(), null, default))
            .Callback<BsonDocument, InsertOneOptions, CancellationToken>((document, _, _) => saved = document);

        Assert.True(_repository.TryAdd(Entry()));

        Assert.NotNull(saved);
        Assert.Equal(6, saved!.ElementCount);
        Assert.Equal("audit-test", saved["_id"].AsString);
        Assert.Equal(10, saved["clinicId"].AsInt64);
        Assert.Equal(100, saved["actorId"].AsInt64);
        Assert.Equal("Created", saved["operation"].AsString);
        Assert.Equal(Entry().OccurredAtUtc, saved["occurredAtUtc"].AsBsonDateTime.ToUniversalTime());
        Assert.True(saved["traceId"].IsBsonNull);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryAdd_QuandoMongoFalha_DeveRetornarFalse(bool timeout)
    {
        Exception error = timeout ? new TimeoutException() : new MongoException("Falha simulada");
        _collection.Setup(c => c.InsertOne(It.IsAny<BsonDocument>(), null, default)).Throws(error);

        Assert.False(_repository.TryAdd(Entry()));
    }

    [Fact]
    public void TryAdd_NaoDeveOcultarErrosDeProgramacao()
    {
        _collection.Setup(c => c.InsertOne(It.IsAny<BsonDocument>(), null, default))
            .Throws(new InvalidOperationException("Falha inesperada"));

        Assert.Throws<InvalidOperationException>(() => _repository.TryAdd(Entry()));
    }
}
