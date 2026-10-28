using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using ClyvoCare.API;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClyvoCare.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"clyvocare-test-{Guid.NewGuid():N}.pem");

    public CustomWebApplicationFactory()
    {
        File.WriteAllText(_publicKeyPath, _rsa.ExportSubjectPublicKeyInfoPem());
    }

    public HttpClient CreateAuthenticatedClient(long ownerId = 100, string email = "admin@test.com", string role = "ADMIN")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(ownerId, email, role));
        return client;
    }

    public string CreateToken(long ownerId, string email, string role, TimeSpan? lifetime = null,
        string issuer = "clyvocare-api", bool invalidSignature = false, bool includeOwnerId = true)
    {
        using var alternateKey = invalidSignature ? RSA.Create(2048) : null;
        var claims = new List<Claim> { new("sub", email), new("role", role) };
        if (includeOwnerId)
            claims.Add(new Claim("ownerId", ownerId.ToString(), ClaimValueTypes.Integer64));
        var token = new JwtSecurityToken(issuer: issuer, claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-1), expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(alternateKey ?? _rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        _connection.Open();
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:PublicKeyPath"] = _publicKeyPath,
                ["Jwt:Issuer"] = "clyvocare-api",
                ["Telemetry:OtlpEnabled"] = "false",
                ["ConnectionStrings:ClyvoCareOracle"] = "Data Source=localhost:1521/TEST;User Id=test;Password=test;"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClinicAuditRepository>();
            services.AddSingleton<IClinicAuditRepository, TestClinicAuditRepository>();
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                // Testa o contrato HTTP sem consultar Oracle, MongoDB, FIAP ou Google.
                var registrations = options.Registrations.ToArray();
                options.Registrations.Clear();
                foreach (var registration in registrations)
                    options.Registrations.Add(new HealthCheckRegistration(registration.Name,
                        _ => new TestDependencyHealthCheck(), registration.FailureStatus, registration.Tags));
            });
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

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
            db.Database.EnsureCreated();
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
        db.SaveChanges();

        // Projeções da Java excluídas das migrations: preparar apenas o schema de teste.
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS TB_CAD_OWNER (ID INTEGER PRIMARY KEY, EMAIL TEXT NOT NULL, ROLE_NAME TEXT NOT NULL, ENABLED TEXT NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS TB_CAD_PET (ID INTEGER PRIMARY KEY, OWNER_ID INTEGER NOT NULL REFERENCES TB_CAD_OWNER(ID))");
        db.Database.ExecuteSqlRaw("INSERT INTO TB_CAD_OWNER (ID,EMAIL,ROLE_NAME,ENABLED) VALUES (100,'admin@test.com','ADMIN','Y'), (200,'owner@test.com','OWNER','Y'), (300,'other@test.com','OWNER','Y')");
        db.Database.ExecuteSqlRaw("INSERT INTO TB_CAD_PET (ID,OWNER_ID) VALUES (1000,200), (2000,300)");
        db.Database.ExecuteSqlRaw("INSERT INTO TB_CAD_CLINIC (ID,NAME,CNPJ,CITY_ID) VALUES (100,'Clinica de testes','10.000.000/0001-00',1)");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
        _rsa.Dispose();
        File.Delete(_publicKeyPath);
    }
}

// Doubles apenas para os testes HTTP com SQLite; não validam persistência MongoDB.
internal sealed class TestClinicAuditRepository : IClinicAuditRepository
{
    public bool TryAdd(ClinicAuditEntry entry) => true;
}

internal sealed class TestDependencyHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Double de teste; dependência externa não consultada."));
}
