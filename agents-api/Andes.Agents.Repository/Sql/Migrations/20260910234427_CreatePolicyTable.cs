using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andes.Agents.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class CreatePolicyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Core");

            migrationBuilder.CreateTable(
                name: "Policy",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyNumber = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ProductCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    HolderReference = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    HolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PremiumAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CoverageAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policy", x => x.Id);
                    table.CheckConstraint("CK_Policy_CoverageAmount", "[CoverageAmount] > 0");
                    table.CheckConstraint("CK_Policy_CoverageWindow", "[ExpirationDate] > [EffectiveDate]");
                    table.CheckConstraint("CK_Policy_CurrencyCode", "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
                    table.CheckConstraint("CK_Policy_PremiumAmount", "[PremiumAmount] >= 0");
                    table.CheckConstraint("CK_Policy_Status", "[Status] COLLATE Latin1_General_100_BIN2 IN ('Draft', 'Active', 'Lapsed', 'Cancelled', 'Expired')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Policy_HolderReference",
                schema: "Core",
                table: "Policy",
                column: "HolderReference")
                .Annotation("SqlServer:Include", new[] { "PolicyNumber", "Status", "EffectiveDate", "ExpirationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Policy_PolicyNumber",
                schema: "Core",
                table: "Policy",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policy_Status_ExpirationDate",
                schema: "Core",
                table: "Policy",
                columns: new[] { "Status", "ExpirationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Policy",
                schema: "Core");
        }
    }
}
