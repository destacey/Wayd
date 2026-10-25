using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Event = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ActorKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reasons = table.Column<int>(type: "int", nullable: false),
                    ReadOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Employees_ActorEmployeeId",
                        column: x => x.ActorEmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_Employees_RecipientEmployeeId",
                        column: x => x.RecipientEmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ActorEmployeeId",
                schema: "Notifications",
                table: "Notifications",
                column: "ActorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventId_RecipientEmployeeId",
                schema: "Notifications",
                table: "Notifications",
                columns: new[] { "EventId", "RecipientEmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OccurredOn",
                schema: "Notifications",
                table: "Notifications",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientEmployeeId_OwnerType_OwnerId_OccurredOn",
                schema: "Notifications",
                table: "Notifications",
                columns: new[] { "RecipientEmployeeId", "OwnerType", "OwnerId", "OccurredOn" })
                .Annotation("SqlServer:Include", new[] { "ReadOn", "Reasons" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Unread",
                schema: "Notifications",
                table: "Notifications",
                columns: new[] { "RecipientEmployeeId", "OwnerType", "OwnerId" },
                filter: "[ReadOn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "Notifications");
        }
    }
}
