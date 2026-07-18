using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.Work.Domain.Models;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class WorkTypeHierarchyConfig : IEntityTypeConfiguration<WorkTypeHierarchy>
{
    public void Configure(EntityTypeBuilder<WorkTypeHierarchy> builder)
    {
        builder.ToTable("WorkTypeHierarchies", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => w.Id);

        // Relationships
        builder.HasMany(builder => builder.Levels)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Levels)
            .AutoInclude();
    }
}

public class WorkTypeLevelConfig : IEntityTypeConfiguration<WorkTypeLevel>
{
    public void Configure(EntityTypeBuilder<WorkTypeLevel> builder)
    {
        builder.ToTable("WorkTypeLevels", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => w.Id);

        // Index for tier lookups
        builder.HasIndex(w => new { w.Id, w.Tier })
            .IncludeProperties(w => w.Order);

        builder.Property(w => w.Name).IsRequired().HasMaxLength(128);
        builder.Property(w => w.Description).HasMaxLength(1024);
        builder.Property(w => w.Tier).IsRequired()
            .HasConversion<EnumConverter<WorkTypeTier>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(w => w.Order);
    }
}

public class WorkItemReferenceConfig : IEntityTypeConfiguration<WorkItemReference>
{
    public void Configure(EntityTypeBuilder<WorkItemReference> builder)
    {
        builder.ToTable("WorkItemReferences", SchemaNames.Work);

        builder.HasKey(w => new { w.WorkItemId, w.ObjectId });

        builder.HasIndex(w => w.WorkItemId)
            .IncludeProperties(w => new { w.ObjectId, w.Context });

        builder.HasIndex(w => new { w.ObjectId, w.Context })
            .IncludeProperties(w => new { w.WorkItemId });

        // Properties
        builder.Property(h => h.Context).IsRequired()
            .HasConversion<EnumConverter<SystemContext>>()
            .HasColumnType("varchar")
            .HasMaxLength(64);

        // Relationships
    }
}

public class WorkItemConfig : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        // Use a unique index instead of alternate key so we can change the Key value when the item changes workspaces
        builder.HasIndex(w => w.Key)
            .IsUnique();

        builder.HasIndex(w => w.Id)
            .IncludeProperties(w => new { w.Key, w.Title, w.WorkspaceId, w.ExternalId, w.AssignedToId, w.TypeId, w.StatusId, w.StatusCategory, w.ActivatedTimestamp, w.DoneTimestamp, w.ProjectId, w.ParentProjectId });

        builder.HasIndex(w => w.Key)
            .IncludeProperties(w => new { w.Id, w.Title, w.WorkspaceId, w.ExternalId, w.AssignedToId, w.TypeId, w.StatusId, w.StatusCategory, w.ActivatedTimestamp, w.DoneTimestamp, w.ProjectId, w.ParentProjectId });

        builder.HasIndex(w => w.WorkspaceId)
            .IncludeProperties(w => new { w.Id, w.Key, w.Title, w.ExternalId, w.AssignedToId, w.TypeId, w.StatusId, w.StatusCategory, w.ActivatedTimestamp, w.DoneTimestamp });

        builder.HasIndex(w => new { w.Key, w.Title })
            .IncludeProperties(w => new { w.Id, w.WorkspaceId, w.ExternalId, w.AssignedToId, w.TypeId, w.StatusId, w.StatusCategory, w.ActivatedTimestamp, w.DoneTimestamp });

        builder.HasIndex(w => w.ExternalId)
            .IncludeProperties(w => new { w.Id, w.Key, w.Title, w.WorkspaceId, w.AssignedToId, w.TypeId, w.StatusId, w.StatusCategory, w.ActivatedTimestamp, w.DoneTimestamp, w.ProjectId, w.ParentProjectId });

        builder.HasIndex(w => w.StatusCategory)
            .IncludeProperties(w => new { w.Id, w.Key, w.Title, w.WorkspaceId, w.AssignedToId, w.TypeId, w.StatusId, w.ActivatedTimestamp, w.DoneTimestamp, w.ProjectId, w.ParentProjectId });

        builder.HasIndex(w => new { w.WorkspaceId, w.ExternalId })
            .IncludeProperties(w => new
            {
                w.Id,
                w.TypeId,
                w.Title,
                w.StatusId,
                w.StatusCategory,
                w.TeamId,
                w.ParentId,
                w.AssignedToId,
                w.Priority,
                w.StackRank,
                w.ActivatedTimestamp,
                w.DoneTimestamp,
                w.LastModified,
                w.LastModifiedById
            })
            .HasFilter("[ExternalId] IS NOT NULL");

        // Index for portfolio parent lookups
        builder.HasIndex(w => new { w.WorkspaceId, w.ExternalId })
            .IncludeProperties(w => new
            {
                w.Id,
                w.ParentId,
                w.TypeId
            })
            .HasFilter("[ExternalId] IS NOT NULL");

        // Covering index on ParentId including Key for the search self-join
        builder.HasIndex(w => w.ParentId)
            .IncludeProperties(w => new { w.Key })
            .HasFilter("[ParentId] IS NOT NULL")
            .HasDatabaseName("IX_WorkItems_ParentId_Key");

        // Composite index for ProjectId and ParentProjectId
        builder.HasIndex(w => new { w.ProjectId, w.ParentProjectId });

        // Filtered index for cases where ProjectId is NULL
        builder.HasIndex(w => w.ParentProjectId)
            .HasFilter("[ProjectId] IS NULL");

        builder.HasIndex(w => w.IterationId)
            .IncludeProperties(w => new { w.Id, w.Key, w.Title, w.WorkspaceId, w.AssignedToId, w.TypeId, w.StatusId, w.StatusCategory, w.ActivatedTimestamp, w.DoneTimestamp, w.ProjectId, w.ParentProjectId });

        // Properties
        builder.Property(w => w.Key).IsRequired()
            .HasConversion(
                w => w.Value,
                w => new WorkItemKey(w))
            .HasColumnType("varchar")
            .HasMaxLength(64);

        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Title).IsRequired().HasMaxLength(256);
        builder.Property(w => w.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<WorkStatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(w => w.ExternalId);
        builder.Property(w => w.Priority);
        builder.Property(w => w.StackRank);
        builder.Property(w => w.StoryPoints);

        builder.Property(w => w.Created);
        builder.Property(w => w.LastModified);
        builder.Property(w => w.ActivatedTimestamp);
        builder.Property(w => w.DoneTimestamp);

        // Persisted computed columns used for server-side key ordering in search
        builder.Property<string>("KeyPrefix")
            .HasComputedColumnSql("LEFT([Key], CHARINDEX('-', [Key]) - 1)", stored: true)
            .HasMaxLength(20);
        builder.Property<int>("KeyNumber")
            .HasComputedColumnSql("CAST(SUBSTRING([Key], CHARINDEX('-', [Key]) + 1, LEN([Key])) AS int)", stored: true);

        builder.HasIndex("KeyPrefix", "KeyNumber")
            .IncludeProperties(w => new { w.Id })
            .HasDatabaseName("IX_WorkItems_KeyPrefix_KeyNumber");

        builder.OwnsMany(w => w.Tags, t =>
        {
            t.ToJson();
            t.UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(w => w.Tags)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(w => w.Tags).Metadata.SetField("_tags");

        // Relationships
        builder.HasOne(w => w.Type)
            .WithMany()
            .HasForeignKey(w => w.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Status)
            .WithMany()
            .HasForeignKey(w => w.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(w => w.Team)
            .WithMany()
            .HasForeignKey(w => w.TeamId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(w => w.AssignedTo)
            .WithMany()
            .HasForeignKey(w => w.AssignedToId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(w => w.CreatedBy)
            .WithMany()
            .HasForeignKey(w => w.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.LastModifiedBy)
            .WithMany()
            .HasForeignKey(w => w.LastModifiedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Map the concrete, typed collections on WorkItem to the corresponding derived
        // WorkItemLink types. These collections are backed by private fields; instruct
        // EF to use field access so it attaches to the backing lists.
        builder.HasMany(w => w.OutboundHierarchies)
            .WithOne(h => h.Source)
            .HasForeignKey(h => h.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(w => w.OutboundHierarchies)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        // Explicitly set backing field so EF can discover the private list
        builder.Navigation(w => w.OutboundHierarchies).Metadata.SetField("_outboundHierarchyHistory");

        builder.HasMany(w => w.InboundHierarchies)
            .WithOne(h => h.Target)
            .HasForeignKey(h => h.TargetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(w => w.InboundHierarchies)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(w => w.InboundHierarchies).Metadata.SetField("_inboundHierarchyHistory");

        builder.HasMany(w => w.OutboundDependencies)
            .WithOne(d => d.Source)
            .HasForeignKey(d => d.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(w => w.OutboundDependencies)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(w => w.OutboundDependencies).Metadata.SetField("_outboundDependencyHistory");

        builder.HasMany(w => w.InboundDependencies)
            .WithOne(d => d.Target)
            .HasForeignKey(d => d.TargetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(w => w.InboundDependencies)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(w => w.InboundDependencies).Metadata.SetField("_inboundDependencyHistory");

        builder.HasMany(w => w.ReferenceLinks)
            .WithOne()
            .HasForeignKey(w => w.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.ExtendedProps)
            .WithOne()
            .HasForeignKey<WorkItemExtended>(w => w.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Project)
            .WithMany()
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(w => w.ParentProject)
            .WithMany()
            .HasForeignKey(w => w.ParentProjectId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(w => w.Iteration)
            .WithMany()
            .HasForeignKey(w => w.IterationId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}

public class WorkItemExtendedConfig : IEntityTypeConfiguration<WorkItemExtended>
{
    public void Configure(EntityTypeBuilder<WorkItemExtended> builder)
    {
        builder.ToTable("WorkItemsExtended", SchemaNames.Work);

        builder.HasIndex(w => new { w.Id, w.ExternalTeamIdentifier });

        // Properties
        builder.Property(w => w.ExternalTeamIdentifier).HasMaxLength(128);
    }
}

public class WorkItemLinkConfig : IEntityTypeConfiguration<WorkItemLink>
{
    public void Configure(EntityTypeBuilder<WorkItemLink> builder)
    {
        builder.ToTable("WorkItemLinks", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        // Configure TPH discriminator using the LinkType property
        builder.HasDiscriminator(w => w.LinkType)
            .HasValue<WorkItemHierarchy>(WorkItemLinkType.Hierarchy)
            .HasValue<WorkItemDependency>(WorkItemLinkType.Dependency);

        builder.HasIndex(w => new { w.SourceId, w.LinkType })
            .IncludeProperties(w => new { w.Id, w.TargetId });

        builder.HasIndex(w => new { w.TargetId, w.LinkType })
            .IncludeProperties(w => new { w.Id, w.SourceId });

        builder.HasIndex(w => new { w.LinkType, w.RemovedOn })
            .IncludeProperties(w => new
            {
                w.SourceId,
                w.TargetId,
                w.CreatedOn,
                w.CreatedById,
                w.Comment
            })
            .HasFilter("[RemovedOn] IS NULL AND [LinkType] = 'Dependency'");

        // Properties
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.LinkType).IsRequired()
            .HasConversion<EnumConverter<WorkItemLinkType>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(w => w.CreatedOn);
        builder.Property(w => w.RemovedOn);
        builder.Property(w => w.Comment).HasMaxLength(1024);

        // Relationships
        builder.HasOne(w => w.CreatedBy)
            .WithMany()
            .HasForeignKey(w => w.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.RemovedBy)
            .WithMany()
            .HasForeignKey(w => w.RemovedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Source/Target relationships here on the dependent (WorkItemLink)
        // by mapping to the WorkItem inverse collection names. This avoids ambiguity
        // between multiple relationships connecting WorkItem and WorkItemLink.
        // (Placed below in the WorkItemLinkConfig section.)
    }
}

public class WorkItemDependencyConfig : IEntityTypeConfiguration<WorkItemDependency>
{
    public void Configure(EntityTypeBuilder<WorkItemDependency> builder)
    {
        // Base configuration is handled in WorkItemLinkConfig


        // Properties
        builder.Property(w => w.State).IsRequired();

        builder.Property(w => w.Health).IsRequired();

        builder.Property(w => w.SourceStatusCategory).IsRequired();

        builder.Property(w => w.TargetStatusCategory).IsRequired();

        builder.Property(w => w.SourcePlannedOn);

        builder.Property(w => w.TargetPlannedOn);
    }
}

public class WorkIterationConfig : IEntityTypeConfiguration<WorkIteration>
{
    public void Configure(EntityTypeBuilder<WorkIteration> builder)
    {
        builder.ToTable("WorkIterations", SchemaNames.Work);

        builder.HasKey(w => w.Id);
        builder.HasAlternateKey(w => w.Key);

        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Key).ValueGeneratedNever();

        // Properties
        builder.Property(i => i.Name).HasMaxLength(256).IsRequired();

        builder.Property(i => i.Type).IsRequired()
            .HasConversion<EnumConverter<IterationType>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(i => i.State).IsRequired()
            .HasConversion<EnumConverter<IterationState>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // Value Objects
        builder.ComplexProperty(i => i.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start");
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships
        builder.HasOne(i => i.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // TODO: this is needed for the nested mapping to work correctly. Not sure why Mapster doesn't pick it up automatically.
        // WorkIterationNavigationDto with Team property
        builder.Navigation(i => i.Team)
            .AutoInclude();
    }
}
public class WorkProcessConfig : IEntityTypeConfiguration<WorkProcess>
{
    public void Configure(EntityTypeBuilder<WorkProcess> builder)
    {
        builder.ToTable("WorkProcesses", SchemaNames.Work);

        builder.HasKey(w => w.Id);
        builder.HasAlternateKey(w => w.Key);

        builder.HasIndex(i => new { i.Id, i.IsDeleted })
            .IncludeProperties(i => new { i.Key, i.Name, i.ExternalId, i.Ownership, i.IsActive })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(i => new { i.Key, i.IsDeleted })
            .IncludeProperties(i => new { i.Id, i.Name, i.ExternalId, i.Ownership, i.IsActive })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(w => w.Key).ValueGeneratedOnAdd();

        // Properties
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Name).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(1024);

        builder.Property(w => w.Ownership).IsRequired()
            .HasConversion<EnumConverter<Ownership>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(w => w.ExternalId);
        builder.Property(w => w.IsActive);

        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);

        // Relationships
        builder.HasMany(w => w.Schemes)
            .WithOne(w => w.WorkProcess)
            .HasForeignKey(w => w.WorkProcessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkProcessSchemeConfig : IEntityTypeConfiguration<WorkProcessScheme>
{
    public void Configure(EntityTypeBuilder<WorkProcessScheme> builder)
    {
        builder.ToTable("WorkProcessSchemes", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => new { w.Id, w.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(w => new { w.WorkProcessId, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.WorkTypeId, w.WorkflowId, w.IsActive })
            .HasFilter("[IsDeleted] = 0");

        // Properties
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.WorkProcessId);
        builder.Property(w => w.WorkTypeId);
        builder.Property(w => w.WorkflowId);
        builder.Property(w => w.IsActive);

        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);

        // Relationships
        builder.HasOne(w => w.WorkType)
            .WithMany()
            .HasForeignKey(w => w.WorkTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Workflow)
            .WithMany()
            .HasForeignKey(w => w.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkProjectConfig : IEntityTypeConfiguration<WorkProject>
{
    public void Configure(EntityTypeBuilder<WorkProject> builder)
    {
        builder.ToTable("WorkProjects", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).ValueGeneratedNever();

        // Properties
        builder.Property(p => p.Key).IsRequired()
            .HasConversion(
                c => c.Value,
                c => new ProjectKey(c))
            .HasColumnType("varchar")
            .HasMaxLength(20);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Description).HasMaxLength(4096).IsRequired();
    }
}

public class WorkspaceConfig : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces", SchemaNames.Work);

        builder.HasKey(w => w.Id);
        builder.HasAlternateKey(w => w.Key);

        builder.HasIndex(w => new { w.Id, w.IsDeleted })
            .IncludeProperties(w => new { w.Key, w.Name, w.IsActive })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => new { w.Key, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.Name, w.IsActive })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => w.Name).IsUnique();
        builder.HasIndex(w => new { w.IsActive, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.Key, w.Name })
            .HasFilter("[IsDeleted] = 0");

        // Properties
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Key).IsRequired()
            .HasConversion(
                w => w.Value,
                w => new WorkspaceKey(w))
            .HasColumnType("varchar")
            .HasMaxLength(20);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(128);
        builder.Property(w => w.Description).HasMaxLength(1024);
        builder.Property(w => w.ExternalViewWorkItemUrlTemplate).HasMaxLength(256);
        builder.Property(w => w.IsActive);

        // Value Objects
        builder.ComplexProperty(w => w.OwnershipInfo, options =>
        {
            options.Property(o => o.Ownership).HasColumnName("Ownership")
                .HasConversion<EnumConverter<Ownership>>()
                .HasColumnType("varchar")
                .HasMaxLength(32)
                .IsRequired();
            options.Property(o => o.Connector).HasColumnName("Connector")
                .HasConversion<EnumConverter<Connector>>()
                .HasColumnType("varchar")
                .HasMaxLength(32);
            options.Property(o => o.SystemId).HasColumnName("SystemId")
                .HasColumnType("varchar")
                .HasMaxLength(64);
            options.Property(o => o.ExternalId).HasColumnName("ExternalId")
                .HasColumnType("varchar")
                .HasMaxLength(64);
        });


        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);

        // Relationships
        builder.HasOne(w => w.WorkProcess)
            .WithMany(w => w.Workspaces)
            .HasForeignKey(w => w.WorkProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.WorkItems)
            .WithOne(w => w.Workspace)
            .HasForeignKey(w => w.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkStatusConfig : IEntityTypeConfiguration<WorkStatus>
{
    public void Configure(EntityTypeBuilder<WorkStatus> builder)
    {
        builder.ToTable("WorkStatuses", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => new { w.Id, w.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => w.Name).IsUnique();
        builder.HasIndex(w => new { w.IsActive, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.Name })
            .HasFilter("[IsDeleted] = 0");

        // Properties
        builder.Property(w => w.Name).IsRequired().HasMaxLength(64);
        builder.Property(w => w.Description).HasMaxLength(1024);
        builder.Property(w => w.IsActive);

        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);
    }
}
public class WorkTeamConfig : IEntityTypeConfiguration<WorkTeam>
{
    public void Configure(EntityTypeBuilder<WorkTeam> builder)
    {
        builder.ToTable("WorkTeams", SchemaNames.Work);

        builder.HasKey(t => t.Id);
        builder.HasAlternateKey(t => t.Key);

        builder.HasIndex(t => t.Id)
            .IncludeProperties(t => new { t.Key, t.Name, t.Code, t.Type, t.IsActive });
        builder.HasIndex(t => t.Key)
            .IncludeProperties(t => new { t.Id, t.Name, t.Code, t.Type, t.IsActive });
        builder.HasIndex(t => t.Code)
            .IsUnique()
            .IncludeProperties(t => new { t.Id, t.Key, t.Name, t.Type, t.IsActive });
        builder.HasIndex(t => t.IsActive)
            .IncludeProperties(t => new { t.Id, t.Key, t.Name, t.Code, t.Type });

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Key).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);
        builder.Property(o => o.Code).IsRequired()
            .HasConversion(
                o => o.Value,
                o => new TeamCode(o))
            .HasColumnType("varchar")
            .HasMaxLength(10);
        builder.Property(t => t.Type).IsRequired()
            .HasConversion<EnumConverter<TeamType>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(t => t.IsActive);

        // Relationships


        // Ignore
    }
}

public class WorkTypeConfig : IEntityTypeConfiguration<WorkType>
{
    public void Configure(EntityTypeBuilder<WorkType> builder)
    {
        builder.ToTable("WorkTypes", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => new { w.Id, w.IsDeleted })
            .IncludeProperties(w => new { w.Name, w.LevelId, w.IsActive })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(w => w.Name).IsUnique();

        builder.HasIndex(w => new { w.IsActive, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.LevelId, w.Name })
            .HasFilter("[IsDeleted] = 0");

        // Index to help with portfolio tier lookups
        builder.HasIndex(w => w.Id)
            .IncludeProperties(w => new
            {
                w.LevelId,
                w.Name
            });

        // Index for work type lookups by name
        builder.HasIndex(w => new { w.Name, w.IsDeleted })
            .IncludeProperties(w => new
            {
                w.Id,
                w.LevelId
            })
            .HasFilter("[IsDeleted] = 0");

        // Properties
        builder.Property(w => w.Name).IsRequired().HasMaxLength(64);
        builder.Property(w => w.Description).HasMaxLength(1024);
        builder.Property(w => w.IsActive);

        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);

        // Relationships
        builder.HasOne(w => w.Level)
            .WithMany()
            .HasForeignKey(w => w.LevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowConfig : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows", SchemaNames.Work);

        builder.HasKey(w => w.Id);
        builder.HasAlternateKey(w => w.Key);


        builder.HasIndex(w => new { w.Id, w.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => new { w.Key, w.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => new { w.IsActive, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.Key, w.Name })
            .HasFilter("[IsDeleted] = 0");

        // Properties
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).ValueGeneratedOnAdd();
        builder.Property(w => w.Name).IsRequired().HasMaxLength(128);
        builder.Property(w => w.Description).HasMaxLength(1024);
        builder.Property(w => w.Ownership).IsRequired()
            .HasConversion<EnumConverter<Ownership>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(w => w.IsActive);

        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);

        // Relationships
        builder.HasMany(w => w.Schemes)
            .WithOne(s => s.Workflow)
            .HasForeignKey(w => w.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Schemes)
            .AutoInclude();
    }
}

public class WorkflowSchemeConfig : IEntityTypeConfiguration<WorkflowScheme>
{
    public void Configure(EntityTypeBuilder<WorkflowScheme> builder)
    {
        builder.ToTable("WorkflowSchemes", SchemaNames.Work);

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => new { w.Id, w.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => new { w.WorkflowId, w.IsDeleted })
            .IncludeProperties(w => new { w.Id, w.WorkStatusId, w.WorkStatusCategory })
            .HasFilter("[IsDeleted] = 0");

        // Properties
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.WorkStatusCategory).IsRequired()
            .HasConversion<EnumConverter<WorkStatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(w => w.IsActive);

        // Soft Delete
        builder.Property(w => w.Deleted);
        builder.Property(w => w.DeletedBy);
        builder.Property(w => w.IsDeleted);

        // Relationships
        builder.HasOne(w => w.WorkStatus)
            .WithMany()
            .HasForeignKey(ws => ws.WorkStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(w => w.WorkStatus)
            .AutoInclude();
    }
}