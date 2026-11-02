using ClyvoCare.API.Health;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ClyvoCare.API.Extensions;

public static class ClyvoCareMongoExtensions
{
    public static IServiceCollection AddClyvoCareMongo(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>().Bind(configuration.GetSection("Mongo"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Configure Mongo:ConnectionString (Mongo__ConnectionString).")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "Configure Mongo:DatabaseName (Mongo__DatabaseName).")
            .ValidateOnStart();
        services.AddSingleton<IMongoClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(3);
            settings.SocketTimeout = TimeSpan.FromSeconds(3);
            return new MongoClient(settings);
        });
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(
            provider.GetRequiredService<IOptions<MongoOptions>>().Value.DatabaseName));
        services.AddScoped<IClinicAuditRepository, ClinicAuditRepository>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("MongoDB");
        return services;
    }
}

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
}
