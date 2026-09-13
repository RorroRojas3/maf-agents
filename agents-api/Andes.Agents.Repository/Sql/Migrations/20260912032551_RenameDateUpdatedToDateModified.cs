using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andes.Agents.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class RenameDateUpdatedToDateModified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DateUpdated",
                schema: "Core",
                table: "Policy",
                newName: "DateModified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DateModified",
                schema: "Core",
                table: "Policy",
                newName: "DateUpdated");
        }
    }
}
