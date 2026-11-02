using ClyvoCare.API;
using ClyvoCare.Infrastructure.Persistence;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ClyvoCare.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), "clyvocare-test-" + Guid.NewGuid());
    private bool _disposed;
    public TestClinicAuditRepository Audit { get; } = new();

    public CustomWebApplicationFactory()
    {
        Directory.CreateDirectory(_keyDirectory);
        File.WriteAllText(Path.Combine(_keyDirectory, "public.pem"), _signingKey.ExportSubjectPublicKeyInfoPem());
    }

    public string CreateToken(JwtPayload payload, string algorithm = SecurityAlgorithms.RsaSha256) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken(new JwtHeader(new SigningCredentials(new RsaSecurityKey(_signingKey),
            algorithm)), payload));

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(new JwtPayload
        {
            ["iss"] = "clyvocare-api", ["sub"] = "admin@example.com", ["ownerId"] = 1L, ["role"] = "ADMIN",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        }));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:PublicKeyPath"] = Path.Combine(_keyDirectory, "public.pem"),
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:DatabaseName"] = "clyvocare-tests"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClinicAuditRepository>();
            services.AddSingleton<IClinicAuditRepository>(Audit);
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ClyvoCareContext>)
                    || d.ServiceType == typeof(ClyvoCareContext)
                    || (d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition().FullName
                            == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1"
                        && d.ServiceType.GenericTypeArguments[0] == typeof(ClyvoCareContext)))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddDbContext<ClyvoCareContext>(options => options.UseSqlite(_connection));
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                foreach (var check in options.Registrations.ToArray())
                {
                    options.Registrations.Remove(check);
                    options.Registrations.Add(new HealthCheckRegistration(check.Name, new HealthyTestCheck(), null, check.Tags));
                }
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
            db.Database.EnsureCreated();
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE TB_CAD_OWNER (ID INTEGER PRIMARY KEY, EMAIL TEXT NOT NULL, ROLE_NAME TEXT NOT NULL, ENABLED TEXT NOT NULL);
                CREATE TABLE TB_CAD_PET (ID INTEGER PRIMARY KEY, OWNER_ID INTEGER NOT NULL REFERENCES TB_CAD_OWNER(ID));
                INSERT INTO TB_CAD_OWNER VALUES (1, 'admin@example.com', 'ADMIN', 'Y');
                INSERT INTO TB_CAD_OWNER VALUES (2, 'owner@example.com', 'OWNER', 'Y');
                INSERT INTO TB_CAD_OWNER VALUES (3, 'disabled@example.com', 'OWNER', 'N');
                INSERT INTO TB_CAD_OWNER VALUES (4, 'disabled-admin@example.com', 'ADMIN', 'N');
                INSERT INTO TB_CAD_OWNER VALUES (5, 'deactivate@example.com', 'OWNER', 'Y');
                INSERT INTO TB_CAD_OWNER VALUES (6, 'other@example.com', 'OWNER', 'Y');
                INSERT INTO TB_CAD_PET VALUES (1, 2);
                INSERT INTO TB_CAD_PET VALUES (2, 6);
                INSERT INTO TB_CAD_PET VALUES (3, 1);
                """);
            Seed(db);
        });
    }

    private static void Seed(ClyvoCareContext db)
    {
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        db.States.Add(state);
        db.SaveChanges();

        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        db.Cities.Add(city);
        db.Clinics.Add(ClyvoCare.Domain.Entities.Clinic.Create("Booking clinic", "88.888.888/0001-88", city.Id, null));
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
        _connection.Dispose();
        _signingKey.Dispose();
        File.Delete(Path.Combine(_keyDirectory, "public.pem"));
        Directory.Delete(_keyDirectory);
    }

    private sealed class HealthyTestCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(HealthCheckResult.Healthy());
    }
}

public sealed class TestClinicAuditRepository : IClinicAuditRepository
{
    public List<ClinicAuditEntry> Entries { get; } = [];
    public bool Available { get; set; } = true;

    public bool TryAdd(ClinicAuditEntry entry)
    {
        if (!Available) return false;
        Entries.Add(entry);
        return true;
    }
}
