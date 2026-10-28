using System.Diagnostics;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public sealed class ClinicAuditRepository(IMongoDatabase database,
    ILogger<ClinicAuditRepository> logger) : IClinicAuditRepository
{
    public const string CollectionName = "ClinicAuditLogs";

    public bool TryAdd(ClinicAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var activity = PersistenceDiagnostics.Source.StartActivity("ClinicAuditRepository.TryAdd");
        var document = new BsonDocument
        {
            { "_id", entry.Id },
            { "clinicId", entry.ClinicId },
            { "operation", entry.Operation },
            { "actorId", entry.ActorId },
            { "occurredAtUtc", new BsonDateTime(entry.OccurredAtUtc) },
            { "traceId", entry.TraceId is null ? BsonNull.Value : new BsonString(entry.TraceId) }
        };

        try
        {
            database.GetCollection<BsonDocument>(CollectionName).InsertOne(document);
            return true;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            // Não incluir a exceção completa: mensagens do driver podem conter detalhes da conexão.
            logger.LogError("Falha de auditoria {AuditId} na operação {Operation} da clínica {ClinicId}. Tipo {ErrorType}; TraceId {TraceId}",
                entry.Id, entry.Operation, entry.ClinicId, exception.GetType().Name, entry.TraceId);
            return false;
        }
    }
}
