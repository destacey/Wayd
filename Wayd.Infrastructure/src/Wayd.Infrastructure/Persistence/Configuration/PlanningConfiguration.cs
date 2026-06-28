using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class PlanningTeamConfig : IEntityTypeConfiguration<PlanningTeam>
{
    public void Configure(EntityTypeBuilder<PlanningTeam> builder)
    {
        builder.ToTable("PlanningTeams", SchemaNames.Planning);

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
        builder.HasMany<Risk>()
            .WithOne(t => t.Team)
            .HasForeignKey(t => t.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

#region Planning Intervals

public class PlanningIntervalConfig : IEntityTypeConfiguration<PlanningInterval>
{
    public void Configure(EntityTypeBuilder<PlanningInterval> builder)
    {
        builder.ToTable("PlanningIntervals", SchemaNames.Planning);

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => p.Key);

        builder.HasIndex(p => new { p.Id, p.IsDeleted })
            .IncludeProperties(p => new { p.Name, p.Description })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(p => p.Name)
            .IsUnique();

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).ValueGeneratedOnAdd();

        builder.Property(p => p.Name).HasMaxLength(128).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2048);

        // Value Objects
        builder.ComplexProperty(p => p.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start").IsRequired();
            options.Property(d => d.End).HasColumnName("End").IsRequired();
        });

        // Soft Delete
        builder.Property(p => p.Deleted);
        builder.Property(p => p.DeletedBy);
        builder.Property(p => p.IsDeleted);
    }
}

public class PlanningIntervalIterationConfig : IEntityTypeConfiguration<PlanningIntervalIteration>
{
    public void Configure(EntityTypeBuilder<PlanningIntervalIteration> builder)
    {
        builder.ToTable("PlanningIntervalIterations", SchemaNames.Planning);

        builder.HasKey(i => i.Id);
        builder.HasAlternateKey(i => i.Key);

        builder.HasIndex(i => new { i.Id, i.IsDeleted })
            .IncludeProperties(i => new { i.Key, i.PlanningIntervalId, i.Name, i.Category })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(i => new { i.Key, i.IsDeleted })
            .IncludeProperties(i => new { i.Id, i.PlanningIntervalId, i.Name, i.Category })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(i => new { i.PlanningIntervalId, i.IsDeleted })
            .IncludeProperties(i => new { i.Id, i.Key, i.Name, i.Category })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Key).ValueGeneratedOnAdd();

        builder.Property(i => i.Name).HasMaxLength(128).IsRequired();
        builder.Property(i => i.Category).IsRequired()
            .HasConversion<EnumConverter<IterationCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // Value Objects
        builder.ComplexProperty(p => p.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start").IsRequired();
            options.Property(d => d.End).HasColumnName("End").IsRequired();
        });

        // Soft Delete
        builder.Property(i => i.Deleted);
        builder.Property(i => i.DeletedBy);
        builder.Property(i => i.IsDeleted);

        // Relationships
        builder.HasOne(i => i.PlanningInterval)
            .WithMany(p => p.Iterations)
            .HasForeignKey(i => i.PlanningIntervalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanningIntervalObjectiveConfig : IEntityTypeConfiguration<PlanningIntervalObjective>
{
    public void Configure(EntityTypeBuilder<PlanningIntervalObjective> builder)
    {
        builder.ToTable("PlanningIntervalObjectives", SchemaNames.Planning);

        builder.HasKey(o => o.Id);
        builder.HasAlternateKey(o => o.Key);

        builder.HasIndex(o => new { o.Id, o.IsDeleted })
            .IncludeProperties(o => new { o.Key, o.PlanningIntervalId, o.ObjectiveId, o.Type, o.IsStretch })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(o => new { o.Key, o.IsDeleted })
            .IncludeProperties(o => new { o.Id, o.PlanningIntervalId, o.ObjectiveId, o.Type, o.IsStretch })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(o => new { o.PlanningIntervalId, o.IsDeleted })
            .IncludeProperties(o => new { o.Id, o.Key, o.ObjectiveId, o.Type, o.IsStretch })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(o => new { o.ObjectiveId, o.IsDeleted })
            .IncludeProperties(o => new { o.Id, o.Key, o.PlanningIntervalId, o.Type, o.IsStretch })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Key).ValueGeneratedOnAdd();

        builder.Property(o => o.ObjectiveId).IsRequired();
        builder.Property(o => o.Type).IsRequired()
            .HasConversion<EnumConverter<PlanningIntervalObjectiveType>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");
        builder.Property(o => o.Status).IsRequired()
            .HasConversion<EnumConverter<ObjectiveStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");
        builder.Property(o => o.IsStretch).IsRequired();

        // Soft Delete
        builder.Property(o => o.Deleted);
        builder.Property(o => o.DeletedBy);
        builder.Property(o => o.IsDeleted);

        // Relationships
        builder.HasOne<PlanningInterval>()
            .WithMany(p => p.Objectives)
            .HasForeignKey(o => o.PlanningIntervalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.HealthChecks)
            .WithOne()
            .HasForeignKey(h => h.PlanningIntervalObjectiveId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanningIntervalTeamConfig : IEntityTypeConfiguration<PlanningIntervalTeam>
{
    public void Configure(EntityTypeBuilder<PlanningIntervalTeam> builder)
    {
        builder.ToTable("PlanningIntervalTeams", SchemaNames.Planning);

        builder.HasKey(p => new { p.PlanningIntervalId, p.TeamId });

        builder.HasIndex(p => p.PlanningIntervalId)
            .IncludeProperties(p => p.TeamId);

        builder.Property(p => p.PlanningIntervalId).IsRequired();
        builder.Property(p => p.TeamId).IsRequired();

        builder.HasOne<PlanningInterval>()
            .WithMany(p => p.Teams)
            .HasForeignKey(p => p.PlanningIntervalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Team)
            .WithMany(p => p.PlanningIntervalTeams)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

#endregion Planning Intervals

public class RiskConfig : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        builder.ToTable("Risks", SchemaNames.Planning);

        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => r.Key);

        builder.HasIndex(r => new { r.Id, r.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => new { r.Key, r.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => new { r.AssigneeId, r.IsDeleted })
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => new { r.TeamId, r.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();

        builder.Property(r => r.Summary).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1024);
        builder.Property(r => r.ReportedOn).IsRequired();

        builder.Property(r => r.Status).IsRequired()
            .HasConversion<EnumConverter<RiskStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(r => r.Category).IsRequired()
            .HasConversion<EnumConverter<RiskCategory>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(r => r.Impact).IsRequired()
            .HasConversion<EnumConverter<RiskGrade>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(r => r.Likelihood).IsRequired()
            .HasConversion<EnumConverter<RiskGrade>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(r => r.FollowUpDate);
        builder.Property(r => r.Response).HasMaxLength(1024);
        builder.Property(r => r.ClosedDate);

        // Soft Delete
        builder.Property(r => r.Deleted);
        builder.Property(r => r.DeletedBy);
        builder.Property(r => r.IsDeleted);

        // Relationships
        builder.HasOne(p => p.ReportedBy)
            .WithMany()
            .HasForeignKey(p => p.ReportedById)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Assignee)
            .WithMany()
            .HasForeignKey(p => p.AssigneeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

#region Roadmaps

public class RoadmapConfig : IEntityTypeConfiguration<Roadmap>
{
    public void Configure(EntityTypeBuilder<Roadmap> builder)
    {
        builder.ToTable("Roadmaps", SchemaNames.Planning);

        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => r.Key);

        builder.HasIndex(r => new { r.Id, r.Visibility })
            .IncludeProperties(r => new { r.Key, r.Name });

        builder.HasIndex(r => new { r.Key, r.Visibility })
            .IncludeProperties(r => new { r.Id, r.Name });

        builder.HasIndex(r => r.Visibility)
            .IncludeProperties(r => new { r.Id, r.Key, r.Name });

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();

        builder.Property(r => r.Name).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2048);

        builder.Property(r => r.Visibility).IsRequired()
            .HasConversion<EnumConverter<Visibility>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(r => r.State).IsRequired()
            .HasConversion<EnumConverter<RoadmapState>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Value Objects
        builder.ComplexProperty(r => r.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start").IsRequired();
            options.Property(d => d.End).HasColumnName("End").IsRequired();
        });

        // Colors are managed as a whole and never queried independently, so they are stored as a
        // JSON column on the Roadmap rather than a separate table.
        builder.OwnsMany(r => r.Colors, c =>
        {
            c.ToJson();
            c.UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(r => r.Colors)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relationships
        builder.HasMany(r => r.RoadmapManagers)
            .WithOne()
            .HasForeignKey(rm => rm.RoadmapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Items)
            .WithOne()
            .HasForeignKey(i => i.RoadmapId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoadmapManagerConfiguration : IEntityTypeConfiguration<RoadmapManager>
{
    public void Configure(EntityTypeBuilder<RoadmapManager> builder)
    {
        builder.ToTable("RoadmapManagers", SchemaNames.Planning);

        builder.HasKey(rm => new { rm.RoadmapId, rm.ManagerId });

        builder.HasIndex(rm => rm.RoadmapId)
            .IncludeProperties(rm => new { rm.ManagerId });

        builder.HasIndex(rm => new { rm.RoadmapId, rm.ManagerId });

        builder.Property(rm => rm.RoadmapId)
            .IsRequired();

        builder.Property(rm => rm.ManagerId)
            .IsRequired();

        builder.HasOne(rm => rm.Manager)
            .WithMany()
            .HasForeignKey(rm => rm.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BaseRoadmapItemConfiguration : IEntityTypeConfiguration<BaseRoadmapItem>
{
    public void Configure(EntityTypeBuilder<BaseRoadmapItem> builder)
    {
        builder.ToTable("RoadmapItems", SchemaNames.Planning);

        builder.HasKey(ri => ri.Id);

        builder.HasDiscriminator(ri => ri.Type)
            .HasValue<RoadmapActivity>(RoadmapItemType.Activity)
            .HasValue<RoadmapMilestone>(RoadmapItemType.Milestone)
            .HasValue<RoadmapTimebox>(RoadmapItemType.Timebox);

        builder.HasIndex(ri => ri.RoadmapId);

        // Properties
        builder.Property(ri => ri.Id).ValueGeneratedNever();
        builder.Property(ri => ri.Name).HasMaxLength(128).IsRequired();
        builder.Property(ri => ri.Description).HasMaxLength(2048);

        builder.Property(ri => ri.Type);

        builder.Property(ri => ri.Color)
            .HasMaxLength(7)
            .HasColumnType("varchar");
    }
}

public class RoadmapActivityConfiguration : IEntityTypeConfiguration<RoadmapActivity>
{
    public void Configure(EntityTypeBuilder<RoadmapActivity> builder)
    {
        // Properties
        builder.Property(a => a.Order).IsRequired();

        // Value Objects
        builder.ComplexProperty(a => a.DateRange, options =>
        {
            options.Property(a => a.Start)
                .HasColumnName("Start")
                .IsRequired();
            options.Property(a => a.End)
                .HasColumnName("End")
                .IsRequired();
        });

        // Relationships
        builder.HasMany(a => a.Children)
            .WithOne(ri => ri.Parent)
            .HasForeignKey(a => a.ParentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class RoadmapTimeboxConfiguration : IEntityTypeConfiguration<RoadmapTimebox>
{
    public void Configure(EntityTypeBuilder<RoadmapTimebox> builder)
    {
        // Value Objects
        builder.ComplexProperty(a => a.DateRange, options =>
        {
            options.Property(a => a.Start)
                .HasColumnName("Start")
                .IsRequired();
            options.Property(a => a.End)
                .HasColumnName("End")
                .IsRequired();
        });
    }
}

public class RoadmapMilestoneConfiguration : IEntityTypeConfiguration<RoadmapMilestone>
{
    public void Configure(EntityTypeBuilder<RoadmapMilestone> builder)
    {
        // Properties
        builder.Property(e => e.Date)
            .HasColumnName("Start")
            .IsRequired();
    }
}

#endregion Roadmaps

#region Iterations

public class IterationConfig : IEntityTypeConfiguration<Iteration>
{
    public void Configure(EntityTypeBuilder<Iteration> builder)
    {
        builder.ToTable("Iterations", SchemaNames.Planning);

        builder.HasKey(i => i.Id);
        builder.HasAlternateKey(i => i.Key);

        // Properties
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Key).ValueGeneratedOnAdd();
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

        builder.ComplexProperty(i => i.OwnershipInfo, options =>
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

        // Ignore
        builder.Ignore(i => i.ExternalMetadataManager);

        // Relationships
        builder.HasOne(o => o.Team)
            .WithMany(t => t.Iterations)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.ExternalMetadata)
            .WithOne()
            .HasForeignKey(m => m.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class IterationExternalMetadata : IEntityTypeConfiguration<KeyValueObjectMetadata>
{
    public void Configure(EntityTypeBuilder<KeyValueObjectMetadata> builder)
    {
        builder.ToTable("IterationExternalMetadata", SchemaNames.Planning);

        builder.HasKey(m => new { m.ObjectId, m.Name });

        builder.HasIndex(m => m.ObjectId)
            .IncludeProperties(m => new { m.Name, m.Value });

        builder.Property(m => m.ObjectId).IsRequired();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Value).HasMaxLength(4000);
    }
}

public class PlanningIntervalIterationSprintConfig : IEntityTypeConfiguration<PlanningIntervalIterationSprint>
{
    public void Configure(EntityTypeBuilder<PlanningIntervalIterationSprint> builder)
    {
        builder.ToTable("PlanningIntervalIterationSprints", SchemaNames.Planning);

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.PlanningIntervalId)
            .IncludeProperties(s => new { s.PlanningIntervalIterationId, s.SprintId });

        builder.HasIndex(s => s.PlanningIntervalIterationId)
            .IncludeProperties(s => new { s.PlanningIntervalId, s.SprintId });

        builder.HasIndex(s => s.SprintId)
            .IsUnique()
            .IncludeProperties(s => new { s.PlanningIntervalId, s.PlanningIntervalIterationId });

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.PlanningIntervalId).IsRequired();
        builder.Property(s => s.PlanningIntervalIterationId).IsRequired();
        builder.Property(s => s.SprintId).IsRequired();

        // Relationships
        builder.HasOne<PlanningInterval>()
            .WithMany(p => p.IterationSprints)
            .HasForeignKey(s => s.PlanningIntervalId)
            .OnDelete(DeleteBehavior.Cascade); // Cascade to enable deletion when removed from collection

        builder.HasOne(s => s.PlanningIntervalIteration)
            .WithMany(i => i.IterationSprints)
            .HasForeignKey(s => s.PlanningIntervalIterationId)
            .OnDelete(DeleteBehavior.NoAction); // NoAction to avoid multiple cascade paths

        builder.HasOne(s => s.Sprint)
            .WithMany()
            .HasForeignKey(s => s.SprintId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


#endregion Iterations


public class PlanningIntervalObjectiveHealthCheckConfig : IEntityTypeConfiguration<PlanningIntervalObjectiveHealthCheck>
{
    public void Configure(EntityTypeBuilder<PlanningIntervalObjectiveHealthCheck> builder)
    {
        builder.ToTable("PlanningIntervalObjectiveHealthChecks", SchemaNames.Planning);

        builder.HasKey(h => h.Id);

        builder.HasIndex(h => new { h.PlanningIntervalObjectiveId, h.Expiration, h.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.PlanningIntervalObjectiveId).IsRequired();

        builder.Property(h => h.Status).IsRequired()
            .HasConversion<EnumConverter<HealthStatus>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(h => h.ReportedById).IsRequired();
        builder.Property(h => h.ReportedOn).IsRequired();
        builder.Property(h => h.Expiration).IsRequired();
        builder.Property(h => h.Note).HasMaxLength(1024);

        // Soft Delete
        builder.Property(h => h.Deleted);
        builder.Property(h => h.DeletedBy);
        builder.Property(h => h.IsDeleted);

        // Relationships
        builder.HasOne(h => h.ReportedBy)
            .WithMany()
            .HasForeignKey(h => h.ReportedById)
            .OnDelete(DeleteBehavior.NoAction);
    }
}


#region Planning Poker

public class EstimationScaleConfig : IEntityTypeConfiguration<EstimationScale>
{
    public void Configure(EntityTypeBuilder<EstimationScale> builder)
    {
        builder.ToTable("EstimationScales", SchemaNames.Planning);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Description).HasMaxLength(1024);
        builder.Property(e => e.Values)
            .HasColumnName("Values");
        builder.Property(e => e.IsActive).IsRequired();
    }
}

public class PokerSessionConfig : IEntityTypeConfiguration<PokerSession>
{
    public void Configure(EntityTypeBuilder<PokerSession> builder)
    {
        builder.ToTable("PokerSessions", SchemaNames.Planning);

        builder.HasKey(s => s.Id);
        builder.HasAlternateKey(s => s.Key);

        builder.HasIndex(s => s.Key);

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Key).ValueGeneratedOnAdd();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(256);
        builder.Property(s => s.EstimationScaleId).IsRequired();
        builder.Property(s => s.FacilitatorId)
            .IsRequired()
            .HasMaxLength(450);
        builder.Property(s => s.Status).IsRequired()
            .HasConversion<EnumConverter<PokerSessionStatus>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(s => s.ActivatedOn);
        builder.Property(s => s.CompletedOn);

        // Relationships
        builder.HasOne(s => s.EstimationScale)
            .WithMany()
            .HasForeignKey(s => s.EstimationScaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Facilitator)
            .WithMany()
            .HasForeignKey(s => s.FacilitatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Rounds)
            .WithOne()
            .HasForeignKey(r => r.PokerSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Rounds)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PokerRoundConfig : IEntityTypeConfiguration<PokerRound>
{
    public void Configure(EntityTypeBuilder<PokerRound> builder)
    {
        builder.ToTable("PokerRounds", SchemaNames.Planning);

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.PokerSessionId);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.PokerSessionId).IsRequired();
        builder.Property(r => r.Label).HasMaxLength(512);
        builder.Property(r => r.Status).IsRequired()
            .HasConversion<EnumConverter<PokerRoundStatus>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(r => r.ConsensusEstimate).HasMaxLength(32);
        builder.Property(r => r.Order).IsRequired();

        // Relationships
        builder.HasMany(r => r.Votes)
            .WithOne()
            .HasForeignKey(v => v.PokerRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        //builder.Navigation(r => r.Votes)
        //    .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PokerVoteConfig : IEntityTypeConfiguration<PokerVote>
{
    public void Configure(EntityTypeBuilder<PokerVote> builder)
    {
        builder.ToTable("PokerVotes", SchemaNames.Planning);

        builder.HasKey(v => v.Id);

        builder.HasIndex(v => new { v.PokerRoundId, v.ParticipantId }).IsUnique();

        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.PokerRoundId).IsRequired();
        builder.Property(v => v.ParticipantId)
            .IsRequired()
            .HasMaxLength(450);
        builder.Property(v => v.Value).IsRequired().HasMaxLength(32);
        builder.Property(v => v.SubmittedOn).IsRequired();

        // Relationships
        builder.HasOne(v => v.Participant)
            .WithMany()
            .HasForeignKey(v => v.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

#endregion Planning Poker
