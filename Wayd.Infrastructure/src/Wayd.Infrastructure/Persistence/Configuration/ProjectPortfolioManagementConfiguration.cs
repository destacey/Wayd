using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ExpenditureCategoryConfig : IEntityTypeConfiguration<ExpenditureCategory>
{
    public void Configure(EntityTypeBuilder<ExpenditureCategory> builder)
    {
        builder.ToTable("ExpenditureCategories", SchemaNames.ProjectPortfolioManagement);
        builder.HasKey(e => e.Id);


        builder.Property(e => e.Name).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1024).IsRequired();

        builder.Property(e => e.State).IsRequired()
            .HasConversion<EnumConverter<ExpenditureCategoryState>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(e => e.IsCapitalizable).IsRequired();
        builder.Property(e => e.RequiresDepreciation).IsRequired();
        builder.Property(e => e.AccountingCode).HasMaxLength(64);
    }
}

public class PpmStrategicThemeConfig : IEntityTypeConfiguration<StrategicTheme>
{
    public void Configure(EntityTypeBuilder<StrategicTheme> builder)
    {
        builder.ToTable("StrategicThemes", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(s => s.Id);
        builder.HasAlternateKey(s => s.Key);

        builder.HasIndex(s => s.State);

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Key).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1024).IsRequired();

        builder.Property(s => s.State).IsRequired()
            .HasConversion<EnumConverter<StrategicThemeState>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");
    }
}

public class ProjectPortfolioConfiguration : IEntityTypeConfiguration<ProjectPortfolio>
{
    public void Configure(EntityTypeBuilder<ProjectPortfolio> builder)
    {
        builder.ToTable("Portfolios", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => p.Key);

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ScoringModelId);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).ValueGeneratedOnAdd();
        builder.Property(p => p.Name).HasMaxLength(128).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1024).IsRequired();
        builder.Property(p => p.Status).IsRequired()
            .HasConversion<EnumConverter<ProjectPortfolioStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");
        builder.Property(p => p.ScoringModelId);

        // Value Objects
        builder.OwnsOne(r => r.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start");
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships
        builder.HasMany(p => p.Projects)
            .WithOne(prj => prj.Portfolio)
            .HasForeignKey(p => p.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ScoringModel)
            .WithMany()
            .HasForeignKey(p => p.ScoringModelId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Programs)
            .WithOne(prg => prg.Portfolio)
            .HasForeignKey(p => p.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StrategicInitiatives)
            .WithOne(i => i.Portfolio)
            .HasForeignKey(i => i.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Roles)
            .WithOne()
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.ToTable("Programs", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => p.Key);

        builder.HasIndex(p => p.Status);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).ValueGeneratedOnAdd();
        builder.Property(p => p.Name).HasMaxLength(128).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2048).IsRequired();
        builder.Property(p => p.Status).IsRequired()
            .HasConversion<EnumConverter<ProgramStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Value Objects
        builder.OwnsOne(r => r.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start");
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships
        builder.HasMany(p => p.Projects)
            .WithOne(prj => prj.Program)
            .HasForeignKey(p => p.ProgramId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Roles)
            .WithOne()
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StrategicThemeTags)
            .WithOne(t => t.Object)
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(p => p.Id);

        // Unique index on Key
        builder.HasIndex(p => p.Key)
            .IsUnique();

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => new { p.PortfolioId, p.Rank });

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(128).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4096).IsRequired();
        builder.Property(p => p.BusinessCase).HasMaxLength(4096);
        builder.Property(p => p.ExpectedBenefits).HasMaxLength(4096);

        // Value Object for Key
        builder.Property(p => p.Key).IsRequired()
            .HasConversion(
                c => c.Value,
                c => new ProjectKey(c))
            .HasColumnType("varchar")
            .HasMaxLength(20);

        builder.Property(p => p.Status).IsRequired()
            .HasConversion<EnumConverter<ProjectStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(p => p.Rank);

        // Value Objects
        builder.OwnsOne(r => r.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start");
            options.Property(d => d.End).HasColumnName("End");
        });

        // Denormalised current-score summary, stored as owned columns. The score history in
        // ProjectScores remains authoritative; this is a read cache for list/grid views. The scorer is a
        // navigation (not frozen) so the name always reflects the current employee.
        builder.OwnsOne(p => p.CurrentScore, options =>
        {
            options.Property(s => s.Value).HasColumnName("CurrentScoreValue").HasColumnType("decimal(18,4)");
            options.Property(s => s.ScoredOn).HasColumnName("CurrentScoredOn");
            options.Property(s => s.ScoredById).HasColumnName("CurrentScoredById");
            options.Property(s => s.ScoringModelName).HasColumnName("CurrentScoringModelName").HasMaxLength(128);

            options.HasOne(s => s.ScoredBy)
                .WithMany()
                .HasForeignKey(s => s.ScoredById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Relationships
        builder.HasOne(p => p.ExpenditureCategory)
            .WithMany()
            .HasForeignKey(p => p.ExpenditureCategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Roles)
            .WithOne()
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StrategicThemeTags)
            .WithOne(t => t.Object)
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StrategicInitiativeProjects)
            .WithOne(i => i.Project)
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ProjectLifecycle)
            .WithMany()
            .HasForeignKey(p => p.ProjectLifecycleId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Phases)
            .WithOne()
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.HealthChecks)
            .WithOne()
            .HasForeignKey(h => h.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Scores)
            .WithOne()
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

#region Project Scoring

public class ProjectScoreConfiguration : IEntityTypeConfiguration<ProjectScore>
{
    public void Configure(EntityTypeBuilder<ProjectScore> builder)
    {
        builder.ToTable("ProjectScores", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(s => s.Id);

        // Unique so the per-project monotonic Sequence is enforced at the database level: concurrent
        // scores for the same project that compute the same next sequence collide here (one wins, the
        // other fails and can retry) rather than silently producing duplicate sequences.
        builder.HasIndex(s => new { s.ProjectId, s.Sequence }).IsUnique();

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.ProjectId).IsRequired();

        // Snapshot reference — deliberately NOT a foreign key. The source model may be archived or
        // replaced; this score must remain valid and self-contained.
        builder.Property(s => s.ScoringModelId).IsRequired();
        builder.Property(s => s.ScoringModelKey).IsRequired();
        builder.Property(s => s.ScoringModelName).HasMaxLength(128).IsRequired();

        builder.Property(s => s.PrimaryValue).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(s => s.ScoredOn).IsRequired();
        builder.Property(s => s.ScoredById).IsRequired();
        builder.Property(s => s.Sequence).IsRequired();

        // Relationships
        builder.HasOne(s => s.ScoredBy)
            .WithMany()
            .HasForeignKey(s => s.ScoredById)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(s => s.Ratings)
            .WithOne()
            .HasForeignKey(r => r.ProjectScoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Outputs)
            .WithOne()
            .HasForeignKey(o => o.ProjectScoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProjectScoreRatingConfiguration : IEntityTypeConfiguration<ProjectScoreRating>
{
    public void Configure(EntityTypeBuilder<ProjectScoreRating> builder)
    {
        builder.ToTable("ProjectScoreRatings", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.ProjectScoreId);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ProjectScoreId).IsRequired();
        builder.Property(r => r.CriterionId).IsRequired();
        builder.Property(r => r.CriterionName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.CriterionToken).HasColumnType("varchar").HasMaxLength(32).IsRequired();
        builder.Property(r => r.RatingValue).HasColumnType("decimal(9,4)").IsRequired();
        builder.Property(r => r.RatingLevelId);
        builder.Property(r => r.RatingLevelLabel).HasMaxLength(64);
        builder.Property(r => r.Order).IsRequired();
    }
}

public class ProjectScoreOutputConfiguration : IEntityTypeConfiguration<ProjectScoreOutput>
{
    public void Configure(EntityTypeBuilder<ProjectScoreOutput> builder)
    {
        builder.ToTable("ProjectScoreOutputs", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(o => o.Id);

        builder.HasIndex(o => o.ProjectScoreId);

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.ProjectScoreId).IsRequired();
        builder.Property(o => o.Token).HasColumnType("varchar").HasMaxLength(32).IsRequired();
        builder.Property(o => o.Name).HasMaxLength(128).IsRequired();
        builder.Property(o => o.Value).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(o => o.IsPrimary).IsRequired();
        builder.Property(o => o.Order).IsRequired();
    }
}

#endregion Project Scoring

public class ProjectHealthCheckConfiguration : IEntityTypeConfiguration<ProjectHealthCheck>
{
    public void Configure(EntityTypeBuilder<ProjectHealthCheck> builder)
    {
        builder.ToTable("ProjectHealthChecks", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(h => h.Id);

        builder.HasIndex(h => new { h.ProjectId, h.Expiration, h.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.ProjectId).IsRequired();

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

public class StrategicInitiativeConfiguration : IEntityTypeConfiguration<StrategicInitiative>
{
    public void Configure(EntityTypeBuilder<StrategicInitiative> builder)
    {
        builder.ToTable("StrategicInitiatives", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(i => i.Id);
        builder.HasAlternateKey(i => i.Key);

        builder.HasIndex(i => i.Status);

        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Key).ValueGeneratedOnAdd();
        builder.Property(i => i.Name).HasMaxLength(128).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(2048).IsRequired();
        builder.Property(i => i.Status).IsRequired()
            .HasConversion<EnumConverter<StrategicInitiativeStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Value Objects
        builder.OwnsOne(r => r.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start");
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships
        builder.HasMany(i => i.Roles)
            .WithOne()
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Kpis)
            .WithOne()
            .HasForeignKey(i => i.StrategicInitiativeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StrategicInitiativeKpiConfiguration : IEntityTypeConfiguration<StrategicInitiativeKpi>
{
    public void Configure(EntityTypeBuilder<StrategicInitiativeKpi> builder)
    {
        builder.UseTpcMappingStrategy();
        builder.ToTable("StrategicInitiativeKpis", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(k => k.Id);
        builder.HasAlternateKey(k => k.Key);

        builder.HasIndex(k => k.StrategicInitiativeId);

        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.Key).ValueGeneratedOnAdd();
        builder.Property(k => k.Name).HasMaxLength(64).IsRequired();
        builder.Property(k => k.Description).HasMaxLength(512);
        builder.Property(k => k.StartingValue);
        builder.Property(k => k.TargetValue).IsRequired();
        builder.Property(k => k.ActualValue);
        builder.Property(k => k.Order).IsRequired();

        builder.Property(k => k.Prefix)
            .HasMaxLength(8);

        builder.Property(k => k.Suffix)
            .HasMaxLength(8);

        builder.Property(k => k.TargetDirection).IsRequired()
            .HasConversion<EnumConverter<KpiTargetDirection>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasMany(k => k.Checkpoints)
            .WithOne()
            .HasForeignKey(k => k.KpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(k => k.Measurements)
            .WithOne()
            .HasForeignKey(k => k.KpiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StrategicInitiativeKpiCheckpointConfiguration : IEntityTypeConfiguration<StrategicInitiativeKpiCheckpoint>
{
    public void Configure(EntityTypeBuilder<StrategicInitiativeKpiCheckpoint> builder)
    {
        builder.UseTpcMappingStrategy();
        builder.ToTable("StrategicInitiativeKpiCheckpoints", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(k => k.Id);

        builder.HasIndex(k => k.KpiId);

        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.TargetValue).IsRequired();
        builder.Property(k => k.AtRiskValue);
        builder.Property(k => k.CheckpointDate).IsRequired();
        builder.Property(k => k.DateLabel).HasMaxLength(16).IsRequired();
    }
}

public class StrategicInitiativeKpiMeasurementConfiguration : IEntityTypeConfiguration<StrategicInitiativeKpiMeasurement>
{
    public void Configure(EntityTypeBuilder<StrategicInitiativeKpiMeasurement> builder)
    {
        builder.UseTpcMappingStrategy();
        builder.ToTable("StrategicInitiativeKpiMeasurements", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.KpiId);

        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ActualValue).IsRequired();
        builder.Property(m => m.MeasurementDate).IsRequired();
        builder.Property(m => m.Note).HasMaxLength(1024);

        // Relationships
        builder.HasOne(p => p.MeasuredBy)
            .WithMany()
            .HasForeignKey(p => p.MeasuredById)
            .OnDelete(DeleteBehavior.NoAction);
    }
}


public class StrategicInitiativeProjectConfiguration : IEntityTypeConfiguration<StrategicInitiativeProject>
{
    public void Configure(EntityTypeBuilder<StrategicInitiativeProject> builder)
    {
        builder.ToTable("StrategicInitiativeProjects", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(i => new { i.StrategicInitiativeId, i.ProjectId });

        builder.HasIndex(i => i.StrategicInitiativeId);
        builder.HasIndex(i => i.ProjectId);

        // Relationships
        builder.HasOne(i => i.StrategicInitiative)
            .WithMany(i => i.StrategicInitiativeProjects)
            .HasForeignKey(i => i.StrategicInitiativeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Project)
            .WithMany(i => i.StrategicInitiativeProjects)
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

#region Project Lifecycles

public class ProjectLifecycleConfiguration : IEntityTypeConfiguration<ProjectLifecycle>
{
    public void Configure(EntityTypeBuilder<ProjectLifecycle> builder)
    {
        builder.ToTable("ProjectLifecycles", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(l => l.Id);
        builder.HasAlternateKey(l => l.Key);

        builder.HasIndex(l => l.State);

        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Key).ValueGeneratedOnAdd();
        builder.Property(l => l.Name).HasMaxLength(128).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(1024).IsRequired();

        builder.Property(l => l.State).IsRequired()
            .HasConversion<EnumConverter<ProjectLifecycleState>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasMany(l => l.Phases)
            .WithOne()
            .HasForeignKey(p => p.ProjectLifecycleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProjectLifecyclePhaseConfiguration : IEntityTypeConfiguration<ProjectLifecyclePhase>
{
    public void Configure(EntityTypeBuilder<ProjectLifecyclePhase> builder)
    {
        builder.ToTable("ProjectLifecyclePhases", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.ProjectLifecycleId);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1024).IsRequired();
        builder.Property(p => p.Order).IsRequired();
    }
}

public class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.ToTable("ProjectPhases", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.ProjectId);
        builder.HasIndex(p => p.ProjectLifecyclePhaseId);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1024).IsRequired();
        builder.Property(p => p.Order).IsRequired();

        builder.Property(p => p.Status).IsRequired()
            .HasConversion<EnumConverter<ProjectPortfolioManagement.Domain.Enums.TaskStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(p => p.Progress)
            .HasConversion(
                k => k.Value,
                k => new Progress(k))
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        // Value Objects
        builder.OwnsOne(p => p.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start");
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships
        builder.HasOne<ProjectLifecyclePhase>()
            .WithMany()
            .HasForeignKey(p => p.ProjectLifecyclePhaseId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Roles)
            .WithOne()
            .HasForeignKey(r => r.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

#endregion Project Lifecycles

#region Role Assignments

public class ProjectPortfolioRoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment<ProjectPortfolioRole>>
{
    public void Configure(EntityTypeBuilder<RoleAssignment<ProjectPortfolioRole>> builder)
    {
        builder.ToTable("PortfolioRoleAssignments", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(r => new { r.ObjectId, r.EmployeeId, r.Role });

        builder.HasIndex(r => r.ObjectId);
        builder.HasIndex(r => r.EmployeeId);

        builder.Property(p => p.Role).IsRequired()
            .HasConversion<EnumConverter<ProjectPortfolioRole>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasOne(rm => rm.Employee)
            .WithMany()
            .HasForeignKey(rm => rm.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ProgramRoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment<ProgramRole>>
{
    public void Configure(EntityTypeBuilder<RoleAssignment<ProgramRole>> builder)
    {
        builder.ToTable("ProgramRoleAssignments", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(r => new { r.ObjectId, r.EmployeeId, r.Role });

        builder.HasIndex(r => r.ObjectId);
        builder.HasIndex(r => r.EmployeeId);

        builder.Property(p => p.Role).IsRequired()
            .HasConversion<EnumConverter<ProgramRole>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasOne(rm => rm.Employee)
            .WithMany()
            .HasForeignKey(rm => rm.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ProjectRoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment<ProjectRole>>
{
    public void Configure(EntityTypeBuilder<RoleAssignment<ProjectRole>> builder)
    {
        builder.ToTable("ProjectRoleAssignments", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(r => new { r.ObjectId, r.EmployeeId, r.Role });

        builder.HasIndex(r => r.ObjectId);
        builder.HasIndex(r => r.EmployeeId);

        builder.Property(p => p.Role).IsRequired()
            .HasConversion<EnumConverter<ProjectRole>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasOne(rm => rm.Employee)
            .WithMany()
            .HasForeignKey(rm => rm.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class StrategicInitiativeRoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment<StrategicInitiativeRole>>
{
    public void Configure(EntityTypeBuilder<RoleAssignment<StrategicInitiativeRole>> builder)
    {
        builder.ToTable("StrategicInitiativeRoleAssignments", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(r => new { r.ObjectId, r.EmployeeId, r.Role });

        builder.HasIndex(r => r.ObjectId);
        builder.HasIndex(r => r.EmployeeId);

        builder.Property(p => p.Role).IsRequired()
            .HasConversion<EnumConverter<StrategicInitiativeRole>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasOne(rm => rm.Employee)
            .WithMany()
            .HasForeignKey(rm => rm.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ProjectPhaseRoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment<ProjectPhaseRole>>
{
    public void Configure(EntityTypeBuilder<RoleAssignment<ProjectPhaseRole>> builder)
    {
        builder.ToTable("ProjectPhaseAssignments", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(r => new { r.ObjectId, r.EmployeeId, r.Role });

        builder.HasIndex(r => r.ObjectId);
        builder.HasIndex(r => r.EmployeeId);

        builder.Property(p => p.Role).IsRequired()
            .HasConversion<EnumConverter<ProjectPhaseRole>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasOne(rm => rm.Employee)
            .WithMany()
            .HasForeignKey(rm => rm.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

#endregion Role Assignments

#region Strategic Theme Tags

public class ProjectStrategicThemesConfiguration : IEntityTypeConfiguration<StrategicThemeTag<Project>>
{
    public void Configure(EntityTypeBuilder<StrategicThemeTag<Project>> builder)
    {
        builder.ToTable("ProjectStrategicThemes", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(t => new { t.ObjectId, t.StrategicThemeId });

        builder.HasIndex(t => t.ObjectId);
        builder.HasIndex(t => t.StrategicThemeId);

        // Relationships
        builder.HasOne(t => t.StrategicTheme)
            .WithMany()
            .HasForeignKey(t => t.StrategicThemeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ProgramStrategicThemesConfiguration : IEntityTypeConfiguration<StrategicThemeTag<Program>>
{
    public void Configure(EntityTypeBuilder<StrategicThemeTag<Program>> builder)
    {
        builder.ToTable("ProgramStrategicThemes", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(t => new { t.ObjectId, t.StrategicThemeId });

        builder.HasIndex(t => t.ObjectId);
        builder.HasIndex(t => t.StrategicThemeId);

        // Relationships
        builder.HasOne(t => t.StrategicTheme)
            .WithMany()
            .HasForeignKey(t => t.StrategicThemeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

#endregion Strategic Theme Tags

#region PpmTeam

public class PpmTeamConfig : IEntityTypeConfiguration<PpmTeam>
{
    public void Configure(EntityTypeBuilder<PpmTeam> builder)
    {
        builder.ToTable("PpmTeams", SchemaNames.ProjectPortfolioManagement);

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
    }
}

#endregion PpmTeam

#region Project Tasks

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.Key)
            .IsUnique();

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.HasIndex(t => t.Number);
        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.ParentId);
        builder.HasIndex(t => t.Status);

        builder.Property(t => t.Number).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2048);

        // Value Object for TaskKey
        builder.Property(p => p.Key)
            .HasConversion(
                k => k.Value,
                k => new ProjectTaskKey(k))
            .HasColumnType("varchar")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Progress)
            .HasConversion(
                k => k.Value,
                k => new Progress(k))
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(t => t.Type).IsRequired()
            .HasConversion<EnumConverter<ProjectTaskType>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(t => t.Status).IsRequired()
            .HasConversion<EnumConverter<ProjectPortfolioManagement.Domain.Enums.TaskStatus>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(t => t.Priority).IsRequired()
            .HasConversion<EnumConverter<TaskPriority>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.Property(t => t.Order).IsRequired();
        builder.Property(t => t.EstimatedEffortHours).HasColumnType("decimal(18,2)");

        // Value Objects for Date Ranges
        builder.OwnsOne(t => t.PlannedDateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("PlannedStart");
            options.Property(d => d.End).HasColumnName("PlannedEnd");
        });

        builder.Property(t => t.PlannedDate).HasColumnName("PlannedDate");

        // Self-referencing relationship for hierarchy
        builder.HasOne(t => t.Parent)
            .WithMany(t => t.Children)
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // Relationship to ProjectPhase
        builder.HasOne(t => t.ProjectPhase)
            .WithMany()
            .HasForeignKey(t => t.ProjectPhaseId)
            .OnDelete(DeleteBehavior.NoAction);

        // Relationship to Roles
        builder.HasMany(t => t.Roles)
            .WithOne()
            .HasForeignKey(a => a.ObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to Dependencies (successors)
        builder.HasMany(t => t.Successors)
            .WithOne(d => d.Predecessor)
            .HasForeignKey(d => d.PredecessorId)
            .OnDelete(DeleteBehavior.NoAction);

        // Relationship to Dependencies (predecessors)
        builder.HasMany(t => t.Predecessors)
            .WithOne(d => d.Successor)
            .HasForeignKey(d => d.SuccessorId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ProjectTaskDependencyConfiguration : IEntityTypeConfiguration<ProjectTaskDependency>
{
    public void Configure(EntityTypeBuilder<ProjectTaskDependency> builder)
    {
        builder.ToTable("ProjectTaskDependencies", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.HasIndex(d => d.PredecessorId);
        builder.HasIndex(d => d.SuccessorId);
        builder.HasIndex(d => d.RemovedOn);

        builder.Property(d => d.Type).IsRequired()
            .HasConversion<EnumConverter<DependencyType>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");
    }
}

public class TaskAssignmentRoleConfiguration : IEntityTypeConfiguration<RoleAssignment<TaskRole>>
{
    public void Configure(EntityTypeBuilder<RoleAssignment<TaskRole>> builder)
    {
        builder.ToTable("ProjectTaskAssignments", SchemaNames.ProjectPortfolioManagement);

        builder.HasKey(a => new { a.ObjectId, a.EmployeeId, a.Role });

        builder.HasIndex(a => a.ObjectId);
        builder.HasIndex(a => a.EmployeeId);

        builder.Property(a => a.Role).IsRequired()
            .HasConversion<EnumConverter<TaskRole>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

#endregion Project Tasks
