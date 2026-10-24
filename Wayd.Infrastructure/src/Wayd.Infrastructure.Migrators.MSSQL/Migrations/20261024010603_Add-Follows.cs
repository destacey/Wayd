using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Notifications");

            migrationBuilder.CreateTable(
                name: "Follows",
                schema: "Notifications",
                columns: table => new
                {
                    OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follows", x => new { x.OwnerType, x.OwnerId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_Follows_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_EmployeeId_OwnerType_FollowedOn",
                schema: "Notifications",
                table: "Follows",
                columns: new[] { "EmployeeId", "OwnerType", "FollowedOn" })
                .Annotation("SqlServer:Include", new[] { "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Follows",
                schema: "Notifications");
        }
    }
}
