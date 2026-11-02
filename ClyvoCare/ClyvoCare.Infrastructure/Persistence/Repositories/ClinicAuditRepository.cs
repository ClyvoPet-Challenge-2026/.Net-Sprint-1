using System.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public sealed class ClinicAuditRepository(IMongoDatabase database,
    ILogger<ClinicAuditRepository> logger) : IClinicAuditRepository
{
    public bool TryAdd(ClinicAuditEntry entry)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("ClinicAuditRepository.TryAdd", ActivityKind.Client);
        try
        {
            database.GetCollection<BsonDocument>("ClinicAuditLogs").InsertOne(new BsonDocument
            {
                ["clinicId"] = entry.ClinicId,
                ["operation"] = entry.Operation,
                ["actorId"] = entry.ActorId,
                ["occurredAtUtc"] = new BsonDateTime(entry.OccurredAtUtc),
                ["traceId"] = entry.TraceId is null ? BsonNull.Value : new BsonString(entry.TraceId)
            });
            return true;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);
            // Oracle has already committed; never expose connection details or undo that write.
            logger.LogWarning("Clinic audit unavailable for {Operation} on clinic {ClinicId} by actor {ActorId}. Oracle changes remain committed.",
                entry.Operation, entry.ClinicId, entry.ActorId);
            return false;
        }
    }
}
