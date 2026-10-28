using ClyvoCare.Application.Repositories;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;

namespace ClyvoCare.API.Extensions;

public static class ClyvoCareMongoExtensions
{
    public static IServiceCollection AddClyvoCareMongo(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IMongoClient>(_ =>
        {
            var connectionString = configuration["Mongo:ConnectionString"]
                ?? throw new InvalidOperationException("Configure Mongo:ConnectionString para a auditoria.");
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(3);
            settings.SocketTimeout = TimeSpan.FromSeconds(3);
            settings.RetryWrites = false;
            settings.WriteConcern = WriteConcern.WMajority.With(wTimeout: TimeSpan.FromSeconds(3));
            return new MongoClient(settings);
        });
        services.AddSingleton<IMongoDatabase>(provider =>
        {
            var databaseName = configuration["Mongo:DatabaseName"];
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("Configure Mongo:DatabaseName para a auditoria.");
            return provider.GetRequiredService<IMongoClient>().GetDatabase(databaseName);
        });
        services.AddScoped<IClinicAuditRepository, ClinicAuditRepository>();
        return services;
    }
}
