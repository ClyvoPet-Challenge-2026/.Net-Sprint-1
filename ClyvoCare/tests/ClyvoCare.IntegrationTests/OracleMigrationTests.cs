using ClyvoCare.Domain.Entities;
using ClyvoCare.Infrastructure.Migrations;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ClyvoCare.IntegrationTests;

public class OracleMigrationTests
{
    private static ClyvoCareContext CreateContext() => new(new DbContextOptionsBuilder<ClyvoCareContext>()
        .UseOracle("User Id=unused;Password=unused;Data Source=localhost:1521/unused;").Options);

    [Fact]
    public void Migration_RestoredHistory_UsesTheAlreadyAppliedIdentifier()
    {
        using var db = CreateContext();
        Assert.Equal(new[] { "20260523133532_InitialCreate", "20261026190000_AddAppointmentDetails" },
            db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migration_Upgrade_OnlyAddsTheRecordedClinicalColumnsAndIndexes()
    {
        var migration = new AddAppointmentDetails();
        Assert.Equal(4, migration.UpOperations.Count);
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(new[] { "REASON", "NOTES" }, columns.Select(column => column.Name));
        Assert.Equal(new[] { "NVARCHAR2(150)", "NVARCHAR2(1000)" }, columns.Select(column => column.ColumnType));
        Assert.All(columns, column =>
        {
            Assert.True(column.IsNullable);
            Assert.Equal("TB_HEA_CLINICAL_EVENT", column.Table);
        });
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToArray();
        Assert.Equal("IX_EVENT_CLINIC", indexes[0].Name);
        Assert.Equal(new[] { "CLINIC_ID" }, indexes[0].Columns);
        Assert.Equal("UX_EVENT_PET_DATE", indexes[1].Name);
        Assert.Equal(new[] { "PET_ID", "EVENT_DATE" }, indexes[1].Columns);
        Assert.True(indexes[1].IsUnique);
    }

    [Fact]
    public void Migration_SqlGeneration_DoesNotRecreateSharedTables()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(
            "20260523133532_InitialCreate", "20261026190000_AddAppointmentDetails");
        Assert.Contains("ALTER TABLE \"TB_HEA_CLINICAL_EVENT\" ADD", sql);
        Assert.Contains("UX_EVENT_PET_DATE", sql);
        Assert.DoesNotContain("CREATE TABLE \"TB_", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("DELETE FROM", sql);
    }

    [Fact]
    public void Migration_JavaReferences_AreExcludedFromSchemaChanges()
    {
        using var db = CreateContext();
        var model = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
        Assert.True(model.FindEntityType(typeof(OwnerReference))!.IsTableExcludedFromMigrations());
        Assert.True(model.FindEntityType(typeof(PetReference))!.IsTableExcludedFromMigrations());
        Assert.All(model.FindEntityType(typeof(Appointment))!.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Migration_Rollback_OnlyRemovesAddedDetailsAndIndexes()
    {
        var operations = new AddAppointmentDetails().DownOperations;
        Assert.Equal(4, operations.Count);
        Assert.Equal(2, operations.OfType<DropColumnOperation>().Count());
        Assert.Equal(2, operations.OfType<DropIndexOperation>().Count());
        Assert.DoesNotContain(operations, operation => operation is DropTableOperation);
    }
}
