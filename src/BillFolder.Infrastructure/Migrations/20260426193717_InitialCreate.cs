using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillFolder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema inicial é aplicado via db/schema.sql (docker-compose dev e
            // manualmente em prod). Esta migration existe apenas pra dar
            // baseline ao __EFMigrationsHistory; migrations futuras vão diffar
            // contra o ApplicationDbContextModelSnapshot que reflete o schema completo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down vazio: revert da baseline não é suportado.
        }
    }
}
