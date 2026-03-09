using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace geoback.Migrations
{
    /// <inheritdoc />
    public partial class SyncLockFieldsWithDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration is intentionally empty because the columns were added manually
            // We're just updating the EF model snapshot to match the database
            migrationBuilder.Sql(@"
                SELECT 'Lock fields already exist in database - updating EF model only' as message;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty down migration to prevent accidental data loss
            migrationBuilder.Sql(@"
                SELECT 'Down migration not supported - keeping lock fields' as message;
            ");
        }
    }
}