using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class StatusWorkflowConfiguration : IEntityTypeConfiguration<StatusWorkflow>
{
    public void Configure(EntityTypeBuilder<StatusWorkflow> builder)
    {
        builder.ToTable("StatusWorkflows", SchemaNames.StatusWorkflows);

        builder.HasKey(w => w.Id);
        builder.HasAlternateKey(w => w.Key);

        builder.HasIndex(w => new { w.OwnerType, w.State })
            .IncludeProperties(w => new { w.Id, w.Key, w.Name, w.IsSystem });

        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Key).ValueGeneratedOnAdd();

        builder.Property(w => w.Name).IsRequired().HasMaxLength(128);
        builder.Property(w => w.Description).HasMaxLength(1024);

        // A registered WorkflowOwnerDescriptor.Key, not an enum: the engine stores owner types it never
        // interprets so that a module can join without changing anything here.
        builder.Property(w => w.OwnerType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(64);

        builder.Property(w => w.State).IsRequired()
            .HasConversion<EnumConverter<StatusWorkflowState>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(w => w.IsSystem).IsRequired();

        // Relationships
        builder.HasMany(w => w.Statuses)
            .WithOne()
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Statuses).HasField("_statuses").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ignore
        builder.Ignore(w => w.RequiredAliases);
        builder.Ignore(w => w.InitialStatus);
    }
}

public class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("WorkflowStatuses", SchemaNames.StatusWorkflows);

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.WorkflowId, s.Name }).IsUnique();

        // Filtered so many statuses may carry NoAlias while a real alias stays unique per workflow.
        builder.HasIndex(s => new { s.WorkflowId, s.Alias })
            .IsUnique()
            .HasFilter($"[Alias] <> {StatusWorkflow.NoAlias}");

        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.WorkflowId).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Description).HasMaxLength(512);

        builder.Property(s => s.Category).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // int, not a converted enum: the meaning is the owning module's, so no single enum could serve
        // every module and a converter would put one module's vocabulary in Infrastructure. Join
        // StatusWorkflows.WorkflowAliasNames to read it. See WorkflowAliasName.
        builder.Property(s => s.Alias).IsRequired();

        builder.Property(s => s.Order).IsRequired();
        builder.Property(s => s.IsSystem).IsRequired();
    }
}

public class WorkflowAssignmentConfiguration : IEntityTypeConfiguration<WorkflowAssignment>
{
    public void Configure(EntityTypeBuilder<WorkflowAssignment> builder)
    {
        builder.ToTable("WorkflowAssignments", SchemaNames.StatusWorkflows);

        builder.HasKey(a => a.Id);

        // One assignment per scope per owner type. The filtered pair covers the org-level default,
        // which SQL Server would otherwise allow to duplicate since NULLs do not compare equal.
        builder.HasIndex(a => new { a.OwnerType, a.ScopeId }).IsUnique().HasFilter("[ScopeId] IS NOT NULL");
        builder.HasIndex(a => a.OwnerType).IsUnique().HasFilter("[ScopeId] IS NULL");
        builder.HasIndex(a => a.WorkflowId);

        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.OwnerType).IsRequired().HasColumnType("varchar").HasMaxLength(64);

        // No foreign key: what a scope is differs per owner type, so no one table could be referenced.
        builder.Property(a => a.ScopeId);

        builder.Property(a => a.WorkflowId).IsRequired();

        builder.HasOne<StatusWorkflow>()
            .WithMany()
            .HasForeignKey(a => a.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StatusTransitionConfiguration : IEntityTypeConfiguration<StatusTransition>
{
    public void Configure(EntityTypeBuilder<StatusTransition> builder)
    {
        builder.ToTable("StatusTransitions", SchemaNames.StatusWorkflows);

        builder.HasKey(t => t.Id);

        // Makes a duplicate sequence impossible: two concurrent transitions that read the same count
        // collide here, so one commits and the other's save is rejected whole.
        //
        // There is no retry — nothing catches the unique violation — so the loser gets a generic error
        // and its status change genuinely did not apply. That is the deliberate trade: two people
        // restatusing one record in the same instant is rare, and a corrupted history is worse than a
        // failed save. A retry would mean catching 2601/2627 here and re-reading the count.
        builder.HasIndex(t => new { t.OwnerType, t.RecordId, t.Sequence }).IsUnique();

        builder.HasIndex(t => new { t.OwnerType, t.RecordId, t.ChangedOn })
            .IncludeProperties(t => new { t.ToStatusName, t.ToCategory, t.ToAlias });

        builder.HasIndex(t => t.WorkflowId);

        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.OwnerType).IsRequired().HasColumnType("varchar").HasMaxLength(64);

        // No foreign key: one table serves every owner type, so the target lives in a different table
        // per module.
        builder.Property(t => t.RecordId).IsRequired();

        builder.Property(t => t.WorkflowId).IsRequired();

        builder.Property(t => t.FromStatusId);
        builder.Property(t => t.FromStatusName).HasMaxLength(64);
        builder.Property(t => t.FromCategory)
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(t => t.ToStatusId).IsRequired();
        builder.Property(t => t.ToStatusName).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ToCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // int rather than a converted enum — see WorkflowStatusConfiguration.Alias.
        builder.Property(t => t.ToAlias).IsRequired();

        builder.Property(t => t.ActorKind).IsRequired()
            .HasConversion<EnumConverter<EventActorKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(t => t.ActorUserId).HasMaxLength(450);

        builder.Property(t => t.ActorEmployeeId);

        // A real foreign key, unlike RecordId — one employee table serves every owner type, so there is
        // a single target to reference. No navigation: the engine holds no module's types, and the read
        // side joins explicitly. NoAction matches ProjectStatusHistory, which references employees the
        // same way.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(t => t.ActorEmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(t => t.ChangedOn).IsRequired();
        builder.Property(t => t.Sequence).IsRequired();
        builder.Property(t => t.Reason).HasMaxLength(1024);
    }
}

public class WorkflowAliasNameConfiguration : IEntityTypeConfiguration<WorkflowAliasName>
{
    public void Configure(EntityTypeBuilder<WorkflowAliasName> builder)
    {
        builder.ToTable("WorkflowAliasNames", SchemaNames.StatusWorkflows);

        builder.HasKey(a => new { a.OwnerType, a.Alias });

        builder.Property(a => a.OwnerType).HasColumnType("varchar").HasMaxLength(64);
        builder.Property(a => a.Alias);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(64);
    }
}
