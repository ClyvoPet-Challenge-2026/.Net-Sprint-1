using ClyvoCare.API.Extensions;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace ClyvoCare.IntegrationTests;

public class OracleConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Validate Connection=false;")]
    public void DbContext_ValidatesPooledConnections_AndPreservesConnectionSettings(string validation)
    {
        const string source = "(DESCRIPTION=(EXPIRE_TIME=1)(ADDRESS=(PROTOCOL=TCP)(HOST=oracle.example)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCL)))";
        var original = new OracleConnectionStringBuilder
        {
            UserID = "test-user", Password = "test;password", DataSource = source, MaxPoolSize = 8
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:ClyvoCareOracle"] = original.ConnectionString + ";" + validation }).Build();
        var services = new ServiceCollection();
        services.AddClyvoCareDbContext(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ClyvoCareContext>();
        var effective = new OracleConnectionStringBuilder(context.Database.GetConnectionString());

        Assert.True(effective.ValidateConnection);
        Assert.Equal(original.UserID, effective.UserID);
        Assert.Equal(original.Password, effective.Password);
        Assert.Equal(source, effective.DataSource);
        Assert.Equal(original.MaxPoolSize, effective.MaxPoolSize);
        Assert.Equal(configuration.GetClyvoCareOracleConnectionString(), effective.ConnectionString);
    }
}
