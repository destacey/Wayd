using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MovePokerRoundSubjectTextIntoLabelAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Widened first: a record's description can be longer than a round's could be.
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            // A round about a record now reads the record's title and description from its own label and
            // description. A description a facilitator wrote on such a round is kept only where the record had none.
            migrationBuilder.Sql("""
                UPDATE [Planning].[PokerRounds]
                SET [Label] = [SubjectTitle],
                    [Description] = COALESCE([SubjectDescription], [Description])
                WHERE [SubjectKind] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "SubjectDescription",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "SubjectTitle",
                schema: "Planning",
                table: "PokerRounds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubjectDescription",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectTitle",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Planning].[PokerRounds]
                SET [SubjectTitle] = [Label],
                    [SubjectDescription] = [Description],
                    [Label] = NULL,
                    [Description] = NULL
                WHERE [SubjectKind] IS NOT NULL;
                """);

            // The narrower column cannot hold a description written since at the wider length.
            migrationBuilder.Sql("""
                UPDATE [Planning].[PokerRounds]
                SET [Description] = LEFT([Description], 2048)
                WHERE LEN([Description]) > 2048;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
