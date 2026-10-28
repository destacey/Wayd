using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MoveRoleNotificationReasons : Migration
    {
        // Participant (4) was produced only for role holders on projects and programs and for the members of an
        // objective's team, which is now Role (64); Participant is left for people given an intake request. Every
        // stored Participant bit, and every mute of it, is therefore a Role one, and moving them keeps each
        // person's mutes meaning what they meant when set. The values are frozen here as the enum has them when
        // this ships. A second run finds no Participant bits and changes nothing.
        //
        // Down is exact only while nothing produces Participant. Once intake requests do, it folds their Role
        // and Participant reasons together, and a person who muted both keeps one mute.

        /// <summary>Moves every stored Participant reason, and every mute of it, to Role.</summary>
        /// <remarks>
        /// A person who already muted Role keeps that mute and loses the Participant one, since the
        /// (EmployeeId, Reason) key allows one row each.
        /// </remarks>
        public const string UpSql = """
            UPDATE [Notifications].[Notifications]
            SET [Reasons] = ([Reasons] & ~4) | 64
            WHERE [Reasons] & 4 <> 0;

            DELETE p
            FROM [Notifications].[ReasonMutes] p
            WHERE p.[Reason] = 4
              AND EXISTS (SELECT 1 FROM [Notifications].[ReasonMutes] r WHERE r.[EmployeeId] = p.[EmployeeId] AND r.[Reason] = 64);

            UPDATE [Notifications].[ReasonMutes]
            SET [Reason] = 64
            WHERE [Reason] = 4;
            """;

        /// <summary>Moves every stored Role reason, and every mute of it, back to Participant.</summary>
        public const string DownSql = """
            UPDATE [Notifications].[Notifications]
            SET [Reasons] = ([Reasons] & ~64) | 4
            WHERE [Reasons] & 64 <> 0;

            DELETE r
            FROM [Notifications].[ReasonMutes] r
            WHERE r.[Reason] = 64
              AND EXISTS (SELECT 1 FROM [Notifications].[ReasonMutes] p WHERE p.[EmployeeId] = r.[EmployeeId] AND p.[Reason] = 4);

            UPDATE [Notifications].[ReasonMutes]
            SET [Reason] = 4
            WHERE [Reason] = 64;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DownSql);
    }
}
