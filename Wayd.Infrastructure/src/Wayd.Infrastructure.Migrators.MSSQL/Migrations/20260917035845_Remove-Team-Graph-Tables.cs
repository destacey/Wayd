using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTeamGraphTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamMembershipEdges",
                schema: "Organization");

            migrationBuilder.DropTable(
                name: "TeamNodes",
                schema: "Organization");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores the tables as Add-Team-Dates-And-Graph-Tables created them — SQL graph NODE and EDGE
        /// tables, which the model cannot describe, so the scaffolded CreateTable is wrong here — but not
        /// their rows: nothing writes them any more.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
               CREATE TABLE [Organization].[TeamNodes] (
                   [Id] UNIQUEIDENTIFIER NOT NULL,
                   [Key] INT NOT NULL,
                   [Name] varchar(128) NOT NULL,
                   [Code] varchar(10) NOT NULL,
                   [Type] varchar(32) NOT NULL,
                   [IsActive] bit NOT NULL DEFAULT 1,
                   [ActiveDate] datetime2(7) NOT NULL,
                   [InactiveDate] datetime2(7) NULL,
                   CONSTRAINT [PK_TeamNodes] PRIMARY KEY ([Id]),
                   CONSTRAINT [AK_TeamNodes_Key] UNIQUE ([Key])
               ) AS NODE;

               CREATE INDEX [IX_TeamNodes_Id] ON [Organization].[TeamNodes] ([Id])
               INCLUDE ([Key], [Name], [Code], [IsActive]);

               CREATE INDEX [IX_TeamNodes_Key] ON [Organization].[TeamNodes] ([Key])
               INCLUDE ([Id], [Name], [Code], [IsActive]);

               CREATE UNIQUE INDEX [IX_TeamNodes_Name] ON [Organization].[TeamNodes] ([Name]);

               CREATE UNIQUE INDEX [IX_TeamNodes_Code] ON [Organization].[TeamNodes] ([Code])
               INCLUDE ([Id], [Key], [Name], [IsActive]);

               CREATE INDEX [IX_TeamNodes_IsActive] ON [Organization].[TeamNodes] ([IsActive]);

               CREATE INDEX [IX_TeamNodes_ActiveDates] ON [Organization].[TeamNodes]
               ([ActiveDate], [InactiveDate]);");

            migrationBuilder.Sql(@"
               CREATE TABLE [Organization].[TeamMembershipEdges] (
                   [Id] UNIQUEIDENTIFIER NOT NULL,
                   [StartDate] datetime2(7) NOT NULL,
                   [EndDate] datetime2(7) NULL,
                   CONSTRAINT [PK_TeamMembershipEdges] PRIMARY KEY ([Id])
               ) AS EDGE;

               CREATE INDEX [IX_TeamMembershipEdges_Active] ON [Organization].[TeamMembershipEdges]
               (StartDate, EndDate)
               INCLUDE ($from_id, $to_id);

               CREATE INDEX [IX_TeamMembershipEdges_DateRange] ON [Organization].[TeamMembershipEdges]
               (EndDate, StartDate)
               INCLUDE ($from_id, $to_id);

               CREATE INDEX [IX_TeamMembershipEdges_FromNode] ON [Organization].[TeamMembershipEdges]
               ($from_id, StartDate, EndDate);

               CREATE INDEX [IX_TeamMembershipEdges_ToNode] ON [Organization].[TeamMembershipEdges]
               ($to_id, StartDate, EndDate);");
        }
    }
}
