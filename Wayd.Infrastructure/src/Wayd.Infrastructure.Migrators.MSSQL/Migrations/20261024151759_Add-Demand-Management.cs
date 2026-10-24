using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "DemandManagement");

            migrationBuilder.CreateTable(
                name: "IntakeChannels",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeDesks",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Visibility = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    IsRequesterSelectable = table.Column<bool>(type: "bit", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    RequesterMayAddParticipants = table.Column<bool>(type: "bit", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDesks", x => x.Id);
                    table.UniqueConstraint("AK_IntakeDesks_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "IntakeDeskReviewers",
                schema: "DemandManagement",
                columns: table => new
                {
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDeskReviewers", x => new { x.DeskId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_IntakeDeskReviewers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeDeskReviewers_IntakeDesks_DeskId",
                        column: x => x.DeskId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeDesks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequests",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedBenefits = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NeedByDate = table.Column<DateTime>(type: "date", nullable: true),
                    NeedByBasis = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    NeedByReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    NeedByCommittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NeedByCommittedOn = table.Column<DateTime>(type: "date", nullable: true),
                    OriginSourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OriginExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OriginUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AssignedReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequests", x => x.Id);
                    table.UniqueConstraint("AK_IntakeRequests_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_IntakeRequests_Employees_AssignedReviewerId",
                        column: x => x.AssignedReviewerId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequests_Employees_NeedByCommittedById",
                        column: x => x.NeedByCommittedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequests_Employees_RequesterId",
                        column: x => x.RequesterId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequests_Employees_SubmittedById",
                        column: x => x.SubmittedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequests_IntakeChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequests_IntakeDesks_DeskId",
                        column: x => x.DeskId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeDesks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequestParticipants",
                schema: "DemandManagement",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestParticipants", x => new { x.RequestId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_IntakeRequestParticipants_Employees_AddedById",
                        column: x => x.AddedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequestParticipants_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequestParticipants_IntakeRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeChannels_Name",
                schema: "DemandManagement",
                table: "IntakeChannels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDeskReviewers_EmployeeId",
                schema: "DemandManagement",
                table: "IntakeDeskReviewers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDesks_Name",
                schema: "DemandManagement",
                table: "IntakeDesks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestParticipants_AddedById",
                schema: "DemandManagement",
                table: "IntakeRequestParticipants",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestParticipants_EmployeeId",
                schema: "DemandManagement",
                table: "IntakeRequestParticipants",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_AssignedReviewerId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "AssignedReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_ChannelId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_DeskId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "DeskId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_NeedByCommittedById",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "NeedByCommittedById");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_OriginSourceSystem_OriginExternalId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                columns: new[] { "OriginSourceSystem", "OriginExternalId" },
                unique: true,
                filter: "[OriginSourceSystem] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_RequesterId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_SubmittedById",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "SubmittedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeDeskReviewers",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequestParticipants",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequests",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeChannels",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeDesks",
                schema: "DemandManagement");
        }
    }
}
