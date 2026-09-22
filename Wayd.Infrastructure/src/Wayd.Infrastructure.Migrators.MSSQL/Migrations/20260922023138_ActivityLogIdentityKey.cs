using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class ActivityLogIdentityKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written. EF scaffolds this as two AlterColumns plus a new EventId column defaulted to
            // Guid.Empty. SQL Server refuses the first two — IDENTITY can only be given to a column as it is
            // created, and a clustered key cannot be retyped in place — and the third would throw away every
            // event id in the table. Those ids are what make a backfill replaying old records collide instead
            // of writing a second copy, so the column is renamed and kept rather than replaced.
            migrationBuilder.Sql(@"
                EXEC sp_rename 'App.ActivityLogs.Id', 'EventId', 'COLUMN';
                ALTER TABLE App.ActivityLogs ADD Id bigint IDENTITY(1,1) NOT NULL;
            ");

            // The related rows reference entries by the old Guid, so the new key is carried across to them
            // before the old column can go. The key and its foreign key have to come off first to free both.
            migrationBuilder.Sql(@"
                ALTER TABLE App.ActivityLogRelatedAggregates
                    DROP CONSTRAINT FK_ActivityLogRelatedAggregates_ActivityLogs_ActivityLogId;
                ALTER TABLE App.ActivityLogRelatedAggregates
                    DROP CONSTRAINT PK_ActivityLogRelatedAggregates;

                ALTER TABLE App.ActivityLogRelatedAggregates ADD ActivityLogKey bigint NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE r
                   SET r.ActivityLogKey = a.Id
                  FROM App.ActivityLogRelatedAggregates r
                  JOIN App.ActivityLogs a ON a.EventId = r.ActivityLogId;
            ");

            // A related row whose entry has gone is dropped rather than carried as an orphan; the foreign key
            // it is about to sit under would refuse it anyway.
            migrationBuilder.Sql(@"
                DELETE FROM App.ActivityLogRelatedAggregates WHERE ActivityLogKey IS NULL;

                ALTER TABLE App.ActivityLogRelatedAggregates ALTER COLUMN ActivityLogKey bigint NOT NULL;
                ALTER TABLE App.ActivityLogRelatedAggregates DROP COLUMN ActivityLogId;
                EXEC sp_rename 'App.ActivityLogRelatedAggregates.ActivityLogKey', 'ActivityLogId', 'COLUMN';
            ");

            // The clustered key moves to the identity. Replacing it rebuilds every nonclustered index on the
            // table, which is where the eight bytes saved per row are actually collected: each one carries the
            // clustering key.
            //
            // EventId keeps a unique index. It is the same guarantee the primary key was providing, moved with
            // the name rather than given up.
            migrationBuilder.Sql(@"
                ALTER TABLE App.ActivityLogs DROP CONSTRAINT PK_ActivityLogs;
                ALTER TABLE App.ActivityLogs ADD CONSTRAINT PK_ActivityLogs PRIMARY KEY CLUSTERED (Id);
                CREATE UNIQUE INDEX IX_ActivityLogs_EventId ON App.ActivityLogs (EventId);
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE App.ActivityLogRelatedAggregates
                    ADD CONSTRAINT PK_ActivityLogRelatedAggregates
                    PRIMARY KEY CLUSTERED (ActivityLogId, AggregateType, AggregateId);

                ALTER TABLE App.ActivityLogRelatedAggregates
                    ADD CONSTRAINT FK_ActivityLogRelatedAggregates_ActivityLogs_ActivityLogId
                    FOREIGN KEY (ActivityLogId) REFERENCES App.ActivityLogs (Id) ON DELETE CASCADE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Puts the event id back as the key, which is what the code before this migration reads: a rollback
            // moves the schema and the application together. The identity values go with it and nothing is lost
            // — they were assigned here and never meant anything outside this table.
            migrationBuilder.Sql(@"
                ALTER TABLE App.ActivityLogRelatedAggregates
                    DROP CONSTRAINT FK_ActivityLogRelatedAggregates_ActivityLogs_ActivityLogId;
                ALTER TABLE App.ActivityLogRelatedAggregates
                    DROP CONSTRAINT PK_ActivityLogRelatedAggregates;

                ALTER TABLE App.ActivityLogRelatedAggregates ADD ActivityLogGuid uniqueidentifier NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE r
                   SET r.ActivityLogGuid = a.EventId
                  FROM App.ActivityLogRelatedAggregates r
                  JOIN App.ActivityLogs a ON a.Id = r.ActivityLogId;

                DELETE FROM App.ActivityLogRelatedAggregates WHERE ActivityLogGuid IS NULL;

                ALTER TABLE App.ActivityLogRelatedAggregates ALTER COLUMN ActivityLogGuid uniqueidentifier NOT NULL;
                ALTER TABLE App.ActivityLogRelatedAggregates DROP COLUMN ActivityLogId;
                EXEC sp_rename 'App.ActivityLogRelatedAggregates.ActivityLogGuid', 'ActivityLogId', 'COLUMN';
            ");

            migrationBuilder.Sql(@"
                DROP INDEX IX_ActivityLogs_EventId ON App.ActivityLogs;
                ALTER TABLE App.ActivityLogs DROP CONSTRAINT PK_ActivityLogs;
                ALTER TABLE App.ActivityLogs DROP COLUMN Id;
                EXEC sp_rename 'App.ActivityLogs.EventId', 'Id', 'COLUMN';
                ALTER TABLE App.ActivityLogs ADD CONSTRAINT PK_ActivityLogs PRIMARY KEY CLUSTERED (Id);
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE App.ActivityLogRelatedAggregates
                    ADD CONSTRAINT PK_ActivityLogRelatedAggregates
                    PRIMARY KEY CLUSTERED (ActivityLogId, AggregateType, AggregateId);

                ALTER TABLE App.ActivityLogRelatedAggregates
                    ADD CONSTRAINT FK_ActivityLogRelatedAggregates_ActivityLogs_ActivityLogId
                    FOREIGN KEY (ActivityLogId) REFERENCES App.ActivityLogs (Id) ON DELETE CASCADE;
            ");
        }
    }
}
