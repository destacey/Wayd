using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MoveWorkProcessToOwnershipInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_ExternalId",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_Id_IsDeleted",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_Key_IsDeleted",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                schema: "Work",
                table: "WorkProcesses",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Connector",
                schema: "Work",
                table: "WorkProcesses",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemId",
                schema: "Work",
                table: "WorkProcesses",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            // The column change leaves each id as SQL Server writes a uniqueidentifier, in upper case; the
            // application writes and compares the lower-case form. Azure DevOps is the only source a work
            // process has ever had an external id from.
            migrationBuilder.Sql(
                """
                UPDATE [Work].[WorkProcesses]
                SET [ExternalId] = LOWER([ExternalId]), [Connector] = 'AzureDevOps'
                WHERE [ExternalId] IS NOT NULL;
                """);

            // Not in the EF model, which cannot declare an index over a complex property's column. It is what
            // makes the second of two concurrent setups of one external process fail instead of duplicating it.
            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_ExternalId",
                schema: "Work",
                table: "WorkProcesses",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_Id_IsDeleted",
                schema: "Work",
                table: "WorkProcesses",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Key", "Name", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_Key_IsDeleted",
                schema: "Work",
                table: "WorkProcesses",
                columns: new[] { "Key", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Id", "Name", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Created by hand in Up, so EF does not know to drop it before the column changes back.
            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_ExternalId",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_Id_IsDeleted",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_Key_IsDeleted",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.DropColumn(
                name: "Connector",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.DropColumn(
                name: "SystemId",
                schema: "Work",
                table: "WorkProcesses");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExternalId",
                schema: "Work",
                table: "WorkProcesses",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_ExternalId",
                schema: "Work",
                table: "WorkProcesses",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_Id_IsDeleted",
                schema: "Work",
                table: "WorkProcesses",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Key", "Name", "ExternalId", "Ownership", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_Key_IsDeleted",
                schema: "Work",
                table: "WorkProcesses",
                columns: new[] { "Key", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Id", "Name", "ExternalId", "Ownership", "IsActive" });
        }
    }
}
