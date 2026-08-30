using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddProductManagementAndStatusWorkflows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "Delivery");

        migrationBuilder.EnsureSchema(
            name: "ProductManagement");

        migrationBuilder.EnsureSchema(
            name: "StatusWorkflows");

        migrationBuilder.CreateTable(
            name: "DeploymentEnvironments",
            schema: "Delivery",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Category = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                RingOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeploymentEnvironments", x => x.Id);
                table.UniqueConstraint("AK_DeploymentEnvironments_Key", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "ProductTypes",
            schema: "ProductManagement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                IsReleasable = table.Column<bool>(type: "bit", nullable: false),
                Order = table.Column<int>(type: "int", nullable: false),
                IsSystem = table.Column<bool>(type: "bit", nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductTypes", x => x.Id);
                table.UniqueConstraint("AK_ProductTypes_Key", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "ReleasePackages",
            schema: "Delivery",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Version = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                TargetDate = table.Column<DateTime>(type: "date", nullable: true),
                ReleasedDate = table.Column<DateTime>(type: "date", nullable: true),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReleasePackages", x => x.Id);
                table.UniqueConstraint("AK_ReleasePackages_Key", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "StatusTransitions",
            schema: "StatusWorkflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FromStatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                FromCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                ToStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ToStatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ToCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                ToAlias = table.Column<int>(type: "int", nullable: false),
                ActorKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusTransitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StatusWorkflows",
            schema: "StatusWorkflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                State = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                IsSystem = table.Column<bool>(type: "bit", nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusWorkflows", x => x.Id);
                table.UniqueConstraint("AK_StatusWorkflows_Key", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowAliasNames",
            schema: "StatusWorkflows",
            columns: table => new
            {
                OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                Alias = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowAliasNames", x => new { x.OwnerType, x.Alias });
            });

        migrationBuilder.CreateTable(
            name: "Products",
            schema: "ProductManagement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                ProductTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.UniqueConstraint("AK_Products_Key", x => x.Key);
                table.ForeignKey(
                    name: "FK_Products_ProductTypes_ProductTypeId",
                    column: x => x.ProductTypeId,
                    principalSchema: "ProductManagement",
                    principalTable: "ProductTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Products_Products_ParentId",
                    column: x => x.ParentId,
                    principalSchema: "ProductManagement",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowAssignments",
            schema: "StatusWorkflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowAssignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowAssignments_StatusWorkflows_WorkflowId",
                    column: x => x.WorkflowId,
                    principalSchema: "StatusWorkflows",
                    principalTable: "StatusWorkflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowStatuses",
            schema: "StatusWorkflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                Category = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                Alias = table.Column<int>(type: "int", nullable: false),
                Order = table.Column<int>(type: "int", nullable: false),
                IsSystem = table.Column<bool>(type: "bit", nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowStatuses", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowStatuses_StatusWorkflows_WorkflowId",
                    column: x => x.WorkflowId,
                    principalSchema: "StatusWorkflows",
                    principalTable: "StatusWorkflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ReleasePackageComponents",
            schema: "Delivery",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Version = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReleasePackageComponents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReleasePackageComponents_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "ProductManagement",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReleasePackageComponents_ReleasePackages_PackageId",
                    column: x => x.PackageId,
                    principalSchema: "Delivery",
                    principalTable: "ReleasePackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Releases",
            schema: "Delivery",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Sequence = table.Column<long>(type: "bigint", nullable: true),
                TargetDate = table.Column<DateTime>(type: "date", nullable: true),
                CutDate = table.Column<DateTime>(type: "date", nullable: true),
                ReleasedDate = table.Column<DateTime>(type: "date", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Releases", x => x.Id);
                table.UniqueConstraint("AK_Releases_Key", x => x.Key);
                table.ForeignKey(
                    name: "FK_Releases_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "ProductManagement",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Releases_ReleasePackages_PackageId",
                    column: x => x.PackageId,
                    principalSchema: "Delivery",
                    principalTable: "ReleasePackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Deployments",
            schema: "Delivery",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnvironmentCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                ArtifactId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Deployments", x => x.Id);
                table.UniqueConstraint("AK_Deployments_Key", x => x.Key);
                table.ForeignKey(
                    name: "FK_Deployments_DeploymentEnvironments_EnvironmentId",
                    column: x => x.EnvironmentId,
                    principalSchema: "Delivery",
                    principalTable: "DeploymentEnvironments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Deployments_ReleasePackages_PackageId",
                    column: x => x.PackageId,
                    principalSchema: "Delivery",
                    principalTable: "ReleasePackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Deployments_Releases_ReleaseId",
                    column: x => x.ReleaseId,
                    principalSchema: "Delivery",
                    principalTable: "Releases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeploymentEnvironments_Category_IsActive",
            schema: "Delivery",
            table: "DeploymentEnvironments",
            columns: new[] { "Category", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_DeploymentEnvironments_Name",
            schema: "Delivery",
            table: "DeploymentEnvironments",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Deployments_EnvironmentId_StartedAt",
            schema: "Delivery",
            table: "Deployments",
            columns: new[] { "EnvironmentId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Deployments_PackageId",
            schema: "Delivery",
            table: "Deployments",
            column: "PackageId");

        migrationBuilder.CreateIndex(
            name: "IX_Deployments_ReleaseId",
            schema: "Delivery",
            table: "Deployments",
            column: "ReleaseId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_ParentId",
            schema: "ProductManagement",
            table: "Products",
            column: "ParentId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_ProductTypeId",
            schema: "ProductManagement",
            table: "Products",
            column: "ProductTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_StatusCategory",
            schema: "ProductManagement",
            table: "Products",
            column: "StatusCategory")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name", "ParentId", "ProductTypeId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProductTypes_Name",
            schema: "ProductManagement",
            table: "ProductTypes",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackageComponents_PackageId_ProductId",
            schema: "Delivery",
            table: "ReleasePackageComponents",
            columns: new[] { "PackageId", "ProductId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackageComponents_ProductId",
            schema: "Delivery",
            table: "ReleasePackageComponents",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackages_ReleasedDate",
            schema: "Delivery",
            table: "ReleasePackages",
            column: "ReleasedDate")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Version", "Name", "StatusCategory" });

        migrationBuilder.CreateIndex(
            name: "IX_Releases_PackageId",
            schema: "Delivery",
            table: "Releases",
            column: "PackageId");

        migrationBuilder.CreateIndex(
            name: "IX_Releases_ProductId_ReleasedDate",
            schema: "Delivery",
            table: "Releases",
            columns: new[] { "ProductId", "ReleasedDate" })
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Version", "Name", "Sequence", "StatusCategory" });

        migrationBuilder.CreateIndex(
            name: "IX_Releases_ProductId_Version",
            schema: "Delivery",
            table: "Releases",
            columns: new[] { "ProductId", "Version" });

        migrationBuilder.CreateIndex(
            name: "IX_StatusTransitions_OwnerType_RecordId_ChangedOn",
            schema: "StatusWorkflows",
            table: "StatusTransitions",
            columns: new[] { "OwnerType", "RecordId", "ChangedOn" })
            .Annotation("SqlServer:Include", new[] { "ToStatusName", "ToCategory", "ToAlias" });

        migrationBuilder.CreateIndex(
            name: "IX_StatusTransitions_OwnerType_RecordId_Sequence",
            schema: "StatusWorkflows",
            table: "StatusTransitions",
            columns: new[] { "OwnerType", "RecordId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusTransitions_WorkflowId",
            schema: "StatusWorkflows",
            table: "StatusTransitions",
            column: "WorkflowId");

        migrationBuilder.CreateIndex(
            name: "IX_StatusWorkflows_OwnerType_State",
            schema: "StatusWorkflows",
            table: "StatusWorkflows",
            columns: new[] { "OwnerType", "State" })
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name", "IsSystem" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowAssignments_OwnerType",
            schema: "StatusWorkflows",
            table: "WorkflowAssignments",
            column: "OwnerType",
            unique: true,
            filter: "[ScopeId] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowAssignments_OwnerType_ScopeId",
            schema: "StatusWorkflows",
            table: "WorkflowAssignments",
            columns: new[] { "OwnerType", "ScopeId" },
            unique: true,
            filter: "[ScopeId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowAssignments_WorkflowId",
            schema: "StatusWorkflows",
            table: "WorkflowAssignments",
            column: "WorkflowId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowStatuses_WorkflowId_Alias",
            schema: "StatusWorkflows",
            table: "WorkflowStatuses",
            columns: new[] { "WorkflowId", "Alias" },
            unique: true,
            filter: "[Alias] <> 0");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowStatuses_WorkflowId_Name",
            schema: "StatusWorkflows",
            table: "WorkflowStatuses",
            columns: new[] { "WorkflowId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Deployments",
            schema: "Delivery");

        migrationBuilder.DropTable(
            name: "ReleasePackageComponents",
            schema: "Delivery");

        migrationBuilder.DropTable(
            name: "StatusTransitions",
            schema: "StatusWorkflows");

        migrationBuilder.DropTable(
            name: "WorkflowAliasNames",
            schema: "StatusWorkflows");

        migrationBuilder.DropTable(
            name: "WorkflowAssignments",
            schema: "StatusWorkflows");

        migrationBuilder.DropTable(
            name: "WorkflowStatuses",
            schema: "StatusWorkflows");

        migrationBuilder.DropTable(
            name: "DeploymentEnvironments",
            schema: "Delivery");

        migrationBuilder.DropTable(
            name: "Releases",
            schema: "Delivery");

        migrationBuilder.DropTable(
            name: "StatusWorkflows",
            schema: "StatusWorkflows");

        migrationBuilder.DropTable(
            name: "Products",
            schema: "ProductManagement");

        migrationBuilder.DropTable(
            name: "ReleasePackages",
            schema: "Delivery");

        migrationBuilder.DropTable(
            name: "ProductTypes",
            schema: "ProductManagement");
    }
}
