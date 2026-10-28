using ClyvoCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClyvoCare.IntegrationTests;

// Usa o provedor Oracle para comparar metadados/gerar SQL; não abre conexão.
public sealed class OracleMigrationTests
{
    private static ClyvoCareContext CreateContext() => new(
        new DbContextOptionsBuilder<ClyvoCareContext>()
            .UseOracle("Data Source=localhost:1521/TEST;User Id=test;Password=test;")
            .Options);

    [Fact]
    public void ModeloAtual_DeveCorresponderAoSnapshot()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void MigrationDeAgendamento_DeveGerarApenasEvolucaoDaTabelaExistente()
    {
        using var context = CreateContext();

        var sql = context.GetService<IMigrator>().GenerateScript(
            fromMigration: "20260523133532_InitialCreate",
            toMigration: "20261026190000_AddAppointmentDetails");

        Assert.Contains("TB_HEA_CLINICAL_EVENT", sql);
        Assert.Contains("REASON", sql);
        Assert.Contains("NOTES", sql);
        Assert.Contains("UX_EVENT_PET_DATE", sql);
        Assert.DoesNotContain("CREATE TABLE", sql.ToUpperInvariant());
        Assert.DoesNotContain("DROP TABLE", sql.ToUpperInvariant());
        Assert.DoesNotContain("TB_CAD_OWNER", sql);
        Assert.DoesNotContain("TB_CAD_PET", sql);
    }
}
