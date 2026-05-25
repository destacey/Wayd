using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.OpenAI;
using Wayd.Common.Application.Enums;
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
            .HasValue<OpenAIConnection>(Connector.OpenAI)
            .HasValue<EntraConnection>(Connector.Entra);

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

        //// SystemId and IsSyncEnabled are only for ISyncableConnection types (kept nullable for backwards compatibility)
        //builder.Property<string>("SystemId")
        //    .HasColumnType("varchar")
        //    .HasMaxLength(64)
        //    .IsRequired(false);
        //builder.Property<bool?>("IsSyncEnabled")
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

        builder.Property(c => c.IsSyncEnabled);
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

public class OpenAIConnectionConfig : IEntityTypeConfiguration<OpenAIConnection>
{
    public void Configure(EntityTypeBuilder<OpenAIConnection> builder)
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