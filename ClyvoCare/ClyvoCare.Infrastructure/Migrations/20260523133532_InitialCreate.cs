using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClyvoCare.Infrastructure.Migrations
{
    /// <summary>
    /// Migration de baseline. O schema já existe no Oracle FIAP fix.sql,
    /// então Up()/Down() ficam vazios e esta migration serve apenas como ponto
    /// de partida para as próximas.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
