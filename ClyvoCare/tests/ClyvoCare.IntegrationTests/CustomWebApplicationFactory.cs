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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
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
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
