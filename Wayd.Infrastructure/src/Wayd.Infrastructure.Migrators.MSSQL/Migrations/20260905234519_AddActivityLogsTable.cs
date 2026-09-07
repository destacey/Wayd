using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddActivityLogsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ActivityLogs",
            schema: "App",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                DomainArea = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                AggregateType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_AggregateType_AggregateId_Timestamp",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "AggregateType", "AggregateId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_CorrelationId",
            schema: "App",
            table: "ActivityLogs",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_Timestamp",
            schema: "App",
            table: "ActivityLogs",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_UserId_Timestamp",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "UserId", "Timestamp" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ActivityLogs",
            schema: "App");
    }
}
