using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddKpis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Kpis");

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "Kpis",
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
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kpis",
                schema: "Kpis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Direction = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ExpressedAs = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AtRiskKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    AtRiskValue = table.Column<double>(type: "float", nullable: true),
                    BaselineValue = table.Column<double>(type: "float", nullable: true),
                    BaselineWindowEnd = table.Column<DateTime>(type: "date", nullable: true),
                    BaselineWindowStart = table.Column<DateTime>(type: "date", nullable: true),
                    TargetKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    TargetValue = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kpis", x => x.Id);
                    table.UniqueConstraint("AK_Kpis_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_Kpis_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "Kpis",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Measurements",
                schema: "Kpis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<double>(type: "float", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodRecordKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    PeriodRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MeasuredOn = table.Column<DateTime>(type: "date", nullable: false),
                    MeasuredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SampleSize = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Measurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Measurements_Employees_MeasuredById",
                        column: x => x.MeasuredById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Measurements_Kpis_KpiId",
                        column: x => x.KpiId,
                        principalSchema: "Kpis",
                        principalTable: "Kpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                schema: "Kpis",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_CategoryId",
                schema: "Kpis",
                table: "Kpis",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_SubjectType_SubjectId_Status_Order",
                schema: "Kpis",
                table: "Kpis",
                columns: new[] { "SubjectType", "SubjectId", "Status", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_KpiId_PeriodEnd",
                schema: "Kpis",
                table: "Measurements",
                columns: new[] { "KpiId", "PeriodEnd" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_KpiId_PeriodStart",
                schema: "Kpis",
                table: "Measurements",
                columns: new[] { "KpiId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_MeasuredById",
                schema: "Kpis",
                table: "Measurements",
                column: "MeasuredById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Measurements",
                schema: "Kpis");

            migrationBuilder.DropTable(
                name: "Kpis",
                schema: "Kpis");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "Kpis");
        }
    }
}
