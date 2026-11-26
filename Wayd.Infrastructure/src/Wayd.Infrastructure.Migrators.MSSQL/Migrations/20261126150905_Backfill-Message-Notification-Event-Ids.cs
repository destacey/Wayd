using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <summary>
    /// Points every notification about a posted discussion message at the message itself, as notifications written
    /// from now on are: a posted message's event shares the message's id, which is what lets a notification open it.
    /// </summary>
    /// <remarks>
    /// The message's id is the activity entry's aggregate id for that event. Only notifications change; the activity
    /// log keeps the event's own id, since it is the record of the event as raised.
    /// </remarks>
    public partial class BackfillMessageNotificationEventIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DownSql);

        /// <summary>Moves each notification from the posted event's id to the message's.</summary>
        internal const string UpSql = """
            UPDATE n
            SET n.EventId = a.AggregateId
            FROM Notifications.Notifications n
            INNER JOIN App.ActivityLogs a ON a.EventId = n.EventId
            WHERE a.EventType = 'DiscussionMessagePostedEvent'
                AND a.AggregateType = 'DiscussionMessage'
                AND n.EventId <> a.AggregateId;
            """;

        /// <summary>
        /// Moves each back to its event's id. A notification written after the change already carries an event id
        /// equal to its message's, so it is left as it is.
        /// </summary>
        internal const string DownSql = """
            UPDATE n
            SET n.EventId = a.EventId
            FROM Notifications.Notifications n
            INNER JOIN App.ActivityLogs a ON a.AggregateId = n.EventId
            WHERE a.EventType = 'DiscussionMessagePostedEvent'
                AND a.AggregateType = 'DiscussionMessage'
                AND n.EventId <> a.EventId;
            """;
    }
}
