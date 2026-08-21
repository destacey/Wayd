using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Enums;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ConnectionConfig : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("Connections", SchemaNames.AppIntegration);

        builder.HasKey(c => c.Id);
        builder.HasDiscriminator(c => c.Connector)
            .HasValue<AzureDevOpsBoardsConnection>(Connector.AzureDevOps)
            .HasValue<AzureOpenAIConnection>(Connector.AzureOpenAI)
            .HasValue<EntraConnection>(Connector.Entra)
            .HasValue<WorkdayConnection>(Connector.Workday);

        builder.HasIndex(c => new { c.Id, c.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.Connector, c.IsActive, c.IsDeleted })
            .IncludeProperties(c => new { c.Id, c.Name })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.IsActive, c.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Description).HasMaxLength(1024);
        builder.Property(w => w.Connector).IsRequired()
            .HasConversion<EnumConverter<Connector>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(c => c.IsActive);
        builder.Property(c => c.IsValidConfiguration);

        //// SystemId is only for ISyncableConnection types (kept nullable for backwards compatibility)
        //builder.Property<string>("SystemId")
        //    .HasColumnType("varchar")
        //    .HasMaxLength(64)
        //    .IsRequired(false);

        // Soft Delete
        builder.Property(c => c.Deleted);
        builder.Property(c => c.DeletedBy);
        builder.Property(c => c.IsDeleted);

        // Relationships
    }
}

public class AzureDevOpsBoardsConnectionConfig : IEntityTypeConfiguration<AzureDevOpsBoardsConnection>
{
    public void Configure(EntityTypeBuilder<AzureDevOpsBoardsConnection> builder)
    {
        builder.Property(c => c.Configuration)
            .HasEncryptedJsonConversion()
            .HasColumnName("Configuration");

        builder.OwnsOne(c => c.TeamConfiguration, ownedBuilder =>
        {
            ownedBuilder.ToJson();
            ownedBuilder.OwnsMany(conf => conf.WorkspaceTeams);
        });

        // ISyncableConnection properties
        builder.Property(c => c.SystemId)
            .HasColumnType("varchar")
            .HasMaxLength(64)
            .IsRequired(false);
    }
}

public class AzureOpenAIConnectionConfig : IEntityTypeConfiguration<AzureOpenAIConnection>
{
    public void Configure(EntityTypeBuilder<AzureOpenAIConnection> builder)
    {
        builder.Property(c => c.Configuration)
            .HasEncryptedJsonConversion()
            .HasColumnName("Configuration");
    }
}

public class EntraConnectionConfig : IEntityTypeConfiguration<EntraConnection>
{
    public void Configure(EntityTypeBuilder<EntraConnection> builder)
    {
        builder.Property(c => c.Configuration)
            .HasEncryptedJsonConversion()
            .HasColumnName("Configuration");
    }
}

public class WorkdayConnectionConfig : IEntityTypeConfiguration<WorkdayConnection>
{
    public void Configure(EntityTypeBuilder<WorkdayConnection> builder)
    {
        builder.Property(c => c.Configuration)
            .HasEncryptedJsonConversion()
            .HasColumnName("Configuration");
    }
}

public class SyncRunConfig : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("SyncRuns", SchemaNames.AppIntegration);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ConnectionId).IsRequired();

        builder.Property(r => r.ConnectorType).IsRequired()
            .HasConversion<EnumConverter<Connector>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(r => r.Status).IsRequired()
            .HasConversion<EnumConverter<SyncRunStatus>>()
            .HasColumnType("varchar")
            .HasMaxLength(16);

        builder.Property(r => r.TriggerSource).IsRequired()
            .HasConversion<EnumConverter<SyncTriggerSource>>()
            .HasColumnType("varchar")
            .HasMaxLength(16);

        builder.Property(r => r.SyncType).IsRequired()
            .HasConversion<EnumConverter<SyncType>>()
            .HasColumnType("varchar")
            .HasMaxLength(16);

        builder.Property(r => r.StartedAt).IsRequired();
        builder.Property(r => r.FinishedAt);
        builder.Property(r => r.WorkspacesPlanned);
        builder.Property(r => r.WorkspacesSucceeded);
        builder.Property(r => r.WorkspacesFailed);
        builder.Property(r => r.WorkItemsProcessed);
        builder.Property(r => r.ErrorsCount);
        builder.Property(r => r.ErrorMessage).HasMaxLength(2000);
        builder.Property(r => r.DetailsJson);

        // No FK to Connections — history must survive connection deletion.
        builder.HasIndex(r => r.ConnectionId);
        builder.HasIndex(r => new { r.ConnectionId, r.StartedAt });
        builder.HasIndex(r => new { r.Status, r.StartedAt });
    }
}

public class ExternalIdentityMappingConfig : IEntityTypeConfiguration<ExternalIdentityMapping>
{
    public void Configure(EntityTypeBuilder<ExternalIdentityMapping> builder)
    {
        builder.ToTable("ExternalIdentityMappings", SchemaNames.AppIntegration);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        // No FK to Connections, matching SyncRuns: Connection is soft-deletable, so a constraint
        // would not fire on delete anyway, and an admin's mapping decisions are worth keeping if a
        // connection is removed and re-created. Queries filter on the connection explicitly.
        builder.Property(m => m.ConnectionId).IsRequired();

        builder.Property(m => m.Connector).IsRequired()
            .HasConversion<EnumConverter<Connector>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(m => m.ExternalId).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Email).HasMaxLength(256);
        builder.Property(m => m.DisplayName).HasMaxLength(256);
        builder.Property(m => m.Handle).HasMaxLength(256);

        builder.Property(m => m.Status).IsRequired()
            .HasConversion<EnumConverter<ExternalIdentityMappingStatus>>()
            .HasColumnType("varchar")
            .HasMaxLength(16);

        builder.Property(m => m.LastSeen).IsRequired();

        // One row per identity per connection. Unfiltered: these rows are never soft-deleted, and
        // the sync upserts against this key on every run.
        builder.HasIndex(m => new { m.ConnectionId, m.ExternalId }).IsUnique();

        // Drives the review queue (unmapped first, most recently seen first).
        builder.HasIndex(m => new { m.ConnectionId, m.Status });

        builder.HasIndex(m => m.EmployeeId);

        // An employee delete must not silently drop the mapping row and let the identity quietly
        // re-resolve to nobody; the row stays and returns to the review queue.
        builder.HasOne(m => m.Employee)
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
