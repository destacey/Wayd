using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrailIdentityKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Not an AlterColumn, which is what EF scaffolds and what SQL Server refuses: IDENTITY can only
            // be given to a column as it is created, and a clustered primary key cannot be retyped in place.
            // The column is therefore replaced. The old Guid values are discarded rather than carried over —
            // nothing reads them, no foreign key points at this table, and the one read path orders by
            // DateTime, so they identify nothing that can be asked for.
            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditTrails",
                schema: "Auditing",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "Auditing",
                table: "AuditTrails");

            // Existing rows are numbered as the engine reads them, which is the old Guid order rather than
            // the order they were written. That is acceptable precisely because the key carries no meaning;
            // DateTime is what orders the history.
            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "Auditing",
                table: "AuditTrails",
                type: "bigint",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditTrails",
                schema: "Auditing",
                table: "AuditTrails",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the column the code at this point in the history expects: a rollback moves the
            // schema and the application together, and before this migration Trail.Id is a Guid. The
            // original values cannot come back — nothing recorded them — so new ones are issued. Nothing
            // referenced the old ones, which is what made replacing them safe to begin with.
            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditTrails",
                schema: "Auditing",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "Auditing",
                table: "AuditTrails");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                schema: "Auditing",
                table: "AuditTrails",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditTrails",
                schema: "Auditing",
                table: "AuditTrails",
                column: "Id");
        }
    }
}
