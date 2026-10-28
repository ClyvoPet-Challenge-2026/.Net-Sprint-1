using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClyvoCare.Infrastructure.Migrations;

/// <summary>Evolui a tabela clínica existente no schema compartilhado, sem recriar dados.</summary>
public partial class AddAppointmentDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "REASON", table: "TB_HEA_CLINICAL_EVENT", type: "NVARCHAR2(150)",
            maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "NOTES", table: "TB_HEA_CLINICAL_EVENT", type: "NVARCHAR2(1000)",
            maxLength: 1000, nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_EVENT_CLINIC", table: "TB_HEA_CLINICAL_EVENT", column: "CLINIC_ID");
        migrationBuilder.CreateIndex(
            name: "UX_EVENT_PET_DATE", table: "TB_HEA_CLINICAL_EVENT",
            columns: new[] { "PET_ID", "EVENT_DATE" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_EVENT_CLINIC", table: "TB_HEA_CLINICAL_EVENT");
        migrationBuilder.DropIndex(name: "UX_EVENT_PET_DATE", table: "TB_HEA_CLINICAL_EVENT");
        migrationBuilder.DropColumn(name: "REASON", table: "TB_HEA_CLINICAL_EVENT");
        migrationBuilder.DropColumn(name: "NOTES", table: "TB_HEA_CLINICAL_EVENT");
    }
}
