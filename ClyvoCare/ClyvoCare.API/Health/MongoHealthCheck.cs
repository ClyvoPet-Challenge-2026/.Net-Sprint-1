using System.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ClyvoCare.API.Health;

public sealed class MongoHealthCheck(IMongoDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity("MongoHealthCheck.Ping", ActivityKind.Client);
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);
            return HealthCheckResult.Degraded("MongoDB unavailable; clinic audit may be missing.");
        }
    }
}
