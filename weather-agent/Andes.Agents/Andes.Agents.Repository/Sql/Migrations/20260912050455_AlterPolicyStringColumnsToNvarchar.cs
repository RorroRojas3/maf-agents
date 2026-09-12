using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andes.Agents.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AlterPolicyStringColumnsToNvarchar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Policy_CurrencyCode",
                schema: "Core",
                table: "Policy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Policy_Status",
                schema: "Core",
                table: "Policy");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "Core",
                table: "Policy",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                schema: "Core",
                table: "Policy",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldUnicode: false,
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyNumber",
                schema: "Core",
                table: "Policy",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldUnicode: false,
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "HolderReference",
                schema: "Core",
                table: "Policy",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldUnicode: false,
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "Core",
                table: "Policy",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(3)",
                oldUnicode: false,
                oldFixedLength: true,
                oldMaxLength: 3);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Policy_CurrencyCode",
                schema: "Core",
                table: "Policy",
                sql: "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Policy_Status",
                schema: "Core",
                table: "Policy",
                sql: "[Status] COLLATE Latin1_General_100_BIN2 IN (N'Draft', N'Active', N'Lapsed', N'Cancelled', N'Expired')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Policy_CurrencyCode",
                schema: "Core",
                table: "Policy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Policy_Status",
                schema: "Core",
                table: "Policy");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "Core",
                table: "Policy",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                schema: "Core",
                table: "Policy",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyNumber",
                schema: "Core",
                table: "Policy",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "HolderReference",
                schema: "Core",
                table: "Policy",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "Core",
                table: "Policy",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Policy_CurrencyCode",
                schema: "Core",
                table: "Policy",
                sql: "[CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Policy_Status",
                schema: "Core",
                table: "Policy",
                sql: "[Status] COLLATE Latin1_General_100_BIN2 IN ('Draft', 'Active', 'Lapsed', 'Cancelled', 'Expired')");
        }
    }
}
