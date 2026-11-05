using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Discussions");

            migrationBuilder.CreateTable(
                name: "Channels",
                schema: "Discussions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Employees_LockedById",
                        column: x => x.LockedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "Discussions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "Discussions",
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Employees_AuthorEmployeeId",
                        column: x => x.AuthorEmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Employees_DeletedById",
                        column: x => x.DeletedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ParentMessageId",
                        column: x => x.ParentMessageId,
                        principalSchema: "Discussions",
                        principalTable: "Messages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Mentions",
                schema: "Discussions",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mentions", x => new { x.MessageId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_Mentions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Mentions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "Discussions",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageRevisions",
                schema: "Discussions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReplacedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageRevisions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "Discussions",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_ChannelType_OwnerId",
                schema: "Discussions",
                table: "Channels",
                columns: new[] { "ChannelType", "OwnerId" },
                unique: true,
                filter: "[OwnerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_LockedById",
                schema: "Discussions",
                table: "Channels",
                column: "LockedById");

            migrationBuilder.CreateIndex(
                name: "IX_Mentions_EmployeeId_MentionedOn",
                schema: "Discussions",
                table: "Mentions",
                columns: new[] { "EmployeeId", "MentionedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageRevisions_MessageId",
                schema: "Discussions",
                table: "MessageRevisions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AuthorEmployeeId",
                schema: "Discussions",
                table: "Messages",
                column: "AuthorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChannelId_ParentMessageId_PostedOn",
                schema: "Discussions",
                table: "Messages",
                columns: new[] { "ChannelId", "ParentMessageId", "PostedOn" })
                .Annotation("SqlServer:Include", new[] { "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedById",
                schema: "Discussions",
                table: "Messages",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ParentMessageId",
                schema: "Discussions",
                table: "Messages",
                column: "ParentMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mentions",
                schema: "Discussions");

            migrationBuilder.DropTable(
                name: "MessageRevisions",
                schema: "Discussions");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "Discussions");

            migrationBuilder.DropTable(
                name: "Channels",
                schema: "Discussions");
        }
    }
}
