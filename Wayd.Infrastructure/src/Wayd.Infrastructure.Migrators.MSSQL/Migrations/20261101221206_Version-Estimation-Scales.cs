using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class VersionEstimationScales : Migration
    {
        // Rekeys estimation scales on a uniqueidentifier, keeping the old identity as Key, and moves each scale's
        // values onto version 1 of a new EstimationScaleVersions table, flagging the cards every seeded scale ends with
        // (?, ∞ and ☕) as never being an estimate. Every poker session is re-pointed at the new scale id and pinned to that version 1.
        //
        // Written by hand: EF cannot alter an identity primary key to a uniqueidentifier in place.
        //
        // Values are compared under a binary collation, as the domain compares them: the default collation gives
        // undefined characters such as most emoji no weight, so N'🐘' = N'☕' there. A scale whose values could not
        // make a valid version 1 — fewer than 2 estimates, or a value twice — stops the migration rather than being
        // carried over broken.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DownSql);

        internal const string UpSql = """
            DECLARE @Invalid nvarchar(max) = (
                SELECT STRING_AGG(CAST(s.[Name] AS nvarchar(max)), N', ')
                FROM [Planning].[EstimationScales] s
                WHERE (SELECT COUNT(*) FROM OPENJSON(s.[Values]) j
                       WHERE j.[value] COLLATE Latin1_General_100_BIN2 NOT IN (N'?', N'∞', N'☕')) < 2
                   OR EXISTS (SELECT 1 FROM OPENJSON(s.[Values]) j
                              GROUP BY j.[value] COLLATE Latin1_General_100_BIN2
                              HAVING COUNT(*) > 1));
            IF @Invalid IS NOT NULL
            BEGIN
                DECLARE @Message nvarchar(2048) = LEFT(CONCAT(
                    N'Version-Estimation-Scales cannot make a valid version 1 (at least 2 estimates, no value twice) for: ',
                    @Invalid, N'. Fix their values first.'), 2048);
                THROW 51000, @Message, 1;
            END;

            ALTER TABLE [Planning].[PokerSessions] DROP CONSTRAINT [FK_PokerSessions_EstimationScales_EstimationScaleId];
            DROP INDEX [IX_PokerSessions_EstimationScaleId] ON [Planning].[PokerSessions];

            -- The identity column becomes Key; a new uniqueidentifier takes over as the primary key.
            ALTER TABLE [Planning].[EstimationScales] DROP CONSTRAINT [PK_EstimationScales];
            EXEC sp_rename N'[Planning].[EstimationScales].[Id]', N'Key', N'COLUMN';
            ALTER TABLE [Planning].[EstimationScales] ADD [Id] uniqueidentifier NOT NULL
                CONSTRAINT [DF_EstimationScales_Id] DEFAULT NEWSEQUENTIALID();
            ALTER TABLE [Planning].[EstimationScales] DROP CONSTRAINT [DF_EstimationScales_Id];
            ALTER TABLE [Planning].[EstimationScales] ADD CONSTRAINT [PK_EstimationScales] PRIMARY KEY ([Id]);
            ALTER TABLE [Planning].[EstimationScales] ADD CONSTRAINT [AK_EstimationScales_Key] UNIQUE ([Key]);

            CREATE TABLE [Planning].[EstimationScaleVersions]
            (
                [Id] uniqueidentifier NOT NULL,
                [EstimationScaleId] uniqueidentifier NOT NULL,
                [Number] int NOT NULL,
                [PublishedOn] datetime2 NOT NULL,
                [Values] nvarchar(max) NULL,
                CONSTRAINT [PK_EstimationScaleVersions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EstimationScaleVersions_EstimationScales_EstimationScaleId] FOREIGN KEY ([EstimationScaleId])
                    REFERENCES [Planning].[EstimationScales] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_EstimationScaleVersions_EstimationScaleId_Number]
                ON [Planning].[EstimationScaleVersions] ([EstimationScaleId], [Number]);

            -- Version 1 holds the scale's values in order, as the owned JSON collection EF reads: [{"Value":…,"IsEstimate":…}].
            EXEC(N'
                INSERT INTO [Planning].[EstimationScaleVersions] ([Id], [EstimationScaleId], [Number], [PublishedOn], [Values])
                SELECT NEWID(), s.[Id], 1, s.[SystemCreated],
                    (SELECT j.[value] AS [Value],
                            CAST(CASE WHEN j.[value] COLLATE Latin1_General_100_BIN2 IN (N''?'', N''∞'', N''☕'') THEN 0 ELSE 1 END AS bit) AS [IsEstimate]
                     FROM OPENJSON(s.[Values]) j
                     ORDER BY CAST(j.[key] AS int)
                     FOR JSON PATH)
                FROM [Planning].[EstimationScales] s;');

            ALTER TABLE [Planning].[EstimationScales] DROP COLUMN [Values];

            -- Sessions move from the old int id to the new uniqueidentifier, and are pinned to version 1.
            EXEC sp_rename N'[Planning].[PokerSessions].[EstimationScaleId]', N'EstimationScaleKey', N'COLUMN';
            ALTER TABLE [Planning].[PokerSessions] ADD [EstimationScaleId] uniqueidentifier NULL, [EstimationScaleVersionId] uniqueidentifier NULL;

            EXEC(N'
                UPDATE p
                SET p.[EstimationScaleId] = s.[Id],
                    p.[EstimationScaleVersionId] = v.[Id]
                FROM [Planning].[PokerSessions] p
                INNER JOIN [Planning].[EstimationScales] s ON s.[Key] = p.[EstimationScaleKey]
                INNER JOIN [Planning].[EstimationScaleVersions] v ON v.[EstimationScaleId] = s.[Id] AND v.[Number] = 1;');

            ALTER TABLE [Planning].[PokerSessions] DROP COLUMN [EstimationScaleKey];
            ALTER TABLE [Planning].[PokerSessions] ALTER COLUMN [EstimationScaleId] uniqueidentifier NOT NULL;
            ALTER TABLE [Planning].[PokerSessions] ALTER COLUMN [EstimationScaleVersionId] uniqueidentifier NOT NULL;

            CREATE INDEX [IX_PokerSessions_EstimationScaleId] ON [Planning].[PokerSessions] ([EstimationScaleId]);
            CREATE INDEX [IX_PokerSessions_EstimationScaleVersionId] ON [Planning].[PokerSessions] ([EstimationScaleVersionId]);
            ALTER TABLE [Planning].[PokerSessions] ADD CONSTRAINT [FK_PokerSessions_EstimationScales_EstimationScaleId]
                FOREIGN KEY ([EstimationScaleId]) REFERENCES [Planning].[EstimationScales] ([Id]);
            ALTER TABLE [Planning].[PokerSessions] ADD CONSTRAINT [FK_PokerSessions_EstimationScaleVersions_EstimationScaleVersionId]
                FOREIGN KEY ([EstimationScaleVersionId]) REFERENCES [Planning].[EstimationScaleVersions] ([Id]);
            """;

        // Puts each scale's current version back as its values. Older versions and every card flag are lost.
        internal const string DownSql = """
            ALTER TABLE [Planning].[PokerSessions] DROP CONSTRAINT [FK_PokerSessions_EstimationScaleVersions_EstimationScaleVersionId];
            ALTER TABLE [Planning].[PokerSessions] DROP CONSTRAINT [FK_PokerSessions_EstimationScales_EstimationScaleId];
            DROP INDEX [IX_PokerSessions_EstimationScaleVersionId] ON [Planning].[PokerSessions];
            DROP INDEX [IX_PokerSessions_EstimationScaleId] ON [Planning].[PokerSessions];

            ALTER TABLE [Planning].[PokerSessions] ADD [EstimationScaleKey] int NULL;
            EXEC(N'
                UPDATE p SET p.[EstimationScaleKey] = s.[Key]
                FROM [Planning].[PokerSessions] p
                INNER JOIN [Planning].[EstimationScales] s ON s.[Id] = p.[EstimationScaleId];');
            ALTER TABLE [Planning].[PokerSessions] DROP COLUMN [EstimationScaleVersionId];
            ALTER TABLE [Planning].[PokerSessions] DROP COLUMN [EstimationScaleId];
            EXEC sp_rename N'[Planning].[PokerSessions].[EstimationScaleKey]', N'EstimationScaleId', N'COLUMN';
            ALTER TABLE [Planning].[PokerSessions] ALTER COLUMN [EstimationScaleId] int NOT NULL;

            ALTER TABLE [Planning].[EstimationScales] ADD [Values] nvarchar(max) NULL;
            EXEC(N'
                UPDATE s SET s.[Values] = COALESCE((
                    SELECT N''['' + STRING_AGG(CAST(N''"'' + STRING_ESCAPE(JSON_VALUE(j.[value], ''$.Value''), ''json'') + N''"'' AS nvarchar(max)), N'','')
                        WITHIN GROUP (ORDER BY CAST(j.[key] AS int)) + N'']''
                    FROM [Planning].[EstimationScaleVersions] v
                    CROSS APPLY OPENJSON(v.[Values]) j
                    WHERE v.[EstimationScaleId] = s.[Id]
                      AND v.[Number] = (SELECT MAX(x.[Number]) FROM [Planning].[EstimationScaleVersions] x WHERE x.[EstimationScaleId] = s.[Id])), N''[]'')
                FROM [Planning].[EstimationScales] s;');
            ALTER TABLE [Planning].[EstimationScales] ALTER COLUMN [Values] nvarchar(max) NOT NULL;

            DROP TABLE [Planning].[EstimationScaleVersions];

            ALTER TABLE [Planning].[EstimationScales] DROP CONSTRAINT [AK_EstimationScales_Key];
            ALTER TABLE [Planning].[EstimationScales] DROP CONSTRAINT [PK_EstimationScales];
            ALTER TABLE [Planning].[EstimationScales] DROP COLUMN [Id];
            EXEC sp_rename N'[Planning].[EstimationScales].[Key]', N'Id', N'COLUMN';
            ALTER TABLE [Planning].[EstimationScales] ADD CONSTRAINT [PK_EstimationScales] PRIMARY KEY ([Id]);

            CREATE INDEX [IX_PokerSessions_EstimationScaleId] ON [Planning].[PokerSessions] ([EstimationScaleId]);
            ALTER TABLE [Planning].[PokerSessions] ADD CONSTRAINT [FK_PokerSessions_EstimationScales_EstimationScaleId]
                FOREIGN KEY ([EstimationScaleId]) REFERENCES [Planning].[EstimationScales] ([Id]);
            """;
    }
}
