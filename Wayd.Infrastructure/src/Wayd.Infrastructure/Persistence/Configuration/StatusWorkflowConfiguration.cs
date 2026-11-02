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

        builder.Property(w => w.TransitionMode).IsRequired()
            .HasConversion<EnumConverter<TransitionMode>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // Relationships
        builder.HasMany(w => w.Statuses)
            .WithOne()
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Statuses).HasField("_statuses").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(w => w.Transitions)
            .WithOne()
            .HasForeignKey(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Transitions).HasField("_transitions").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ignore
        builder.Ignore(w => w.RequiredAliases);
        builder.Ignore(w => w.InitialStatus);
        builder.Ignore(w => w.SupportsRestrictedTransitions);
        builder.Ignore(w => w.EnforcesTransitions);
    }
}

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions", SchemaNames.StatusWorkflows);

        builder.HasKey(t => t.Id);

        builder.HasIndex(t => new { t.WorkflowId, t.FromStatusId, t.ToStatusId }).IsUnique();

        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.WorkflowId).IsRequired();
        builder.Property(t => t.FromStatusId).IsRequired();
        builder.Property(t => t.ToStatusId).IsRequired();

        // NoAction rather than Cascade: the workflow already cascades to both the statuses and the
        // transitions, and SQL Server refuses multiple cascade paths. The aggregate removes a status's
        // transitions itself when the status is removed.
        builder.HasOne<WorkflowStatus>()
            .WithMany()
            .HasForeignKey(t => t.FromStatusId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<WorkflowStatus>()
            .WithMany()
            .HasForeignKey(t => t.ToStatusId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(t => t.Conditions)
            .WithOne()
            .HasForeignKey(c => c.TransitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Conditions).HasField("_conditions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class TransitionConditionConfiguration : IEntityTypeConfiguration<TransitionCondition>
{
    public void Configure(EntityTypeBuilder<TransitionCondition> builder)
    {
        builder.ToTable("TransitionConditions", SchemaNames.StatusWorkflows);

        builder.HasKey(c => c.Id);

        // At most one condition of each kind per transition.
        builder.HasIndex(c => new { c.TransitionId, c.Kind }).IsUnique();

        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.TransitionId).IsRequired();

        builder.Property(c => c.Kind).IsRequired()
            .HasConversion<EnumConverter<TransitionConditionKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
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
        builder.HasIndex(a => new { a.OwnerType, a.ScopeId }).IsUnique().WhereNotNull("ScopeId");
        builder.HasIndex(a => a.OwnerType).IsUnique().WhereNull("ScopeId");
        builder.HasIndex(a => a.SchemeId);

        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.OwnerType).IsRequired().HasColumnType("varchar").HasMaxLength(64);

        // No foreign key: what a scope is differs per owner type, so no one table could be referenced.
        builder.Property(a => a.ScopeId);

        builder.Property(a => a.SchemeId).IsRequired();

        builder.HasOne(a => a.Scheme)
            .WithMany()
            .HasForeignKey(a => a.SchemeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecordTypeConfiguration : IEntityTypeConfiguration<RecordType>
{
    public void Configure(EntityTypeBuilder<RecordType> builder)
    {
        builder.ToTable("RecordTypes", SchemaNames.StatusWorkflows);

        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => r.Key);

        builder.HasIndex(r => new { r.OwnerType, r.Name }).IsUnique();

        // A source identifies each of its types once; owned types have no external key.
        builder.HasIndex(r => new { r.OwnerType, r.ExternalKey }).IsUnique().WhereNotNull("ExternalKey");

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();

        builder.Property(r => r.OwnerType).IsRequired().HasColumnType("varchar").HasMaxLength(64);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(128);

        builder.Property(r => r.Ownership).IsRequired()
            .HasConversion<EnumConverter<WorkflowOwnership>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(r => r.ExternalKey).HasMaxLength(256);
        builder.Property(r => r.IsActive).IsRequired();
    }
}

public class StatusWorkflowSchemeConfiguration : IEntityTypeConfiguration<StatusWorkflowScheme>
{
    public void Configure(EntityTypeBuilder<StatusWorkflowScheme> builder)
    {
        builder.ToTable("WorkflowSchemes", SchemaNames.StatusWorkflows);

        builder.HasKey(s => s.Id);
        builder.HasAlternateKey(s => s.Key);

        builder.HasIndex(s => new { s.OwnerType, s.ExternalKey }).IsUnique().WhereNotNull("ExternalKey");

        // One implicit scheme per workflow, shared by every scope that picks the workflow directly.
        builder.HasIndex(s => s.DefaultWorkflowId, "IX_WorkflowSchemes_DefaultWorkflowId_Implicit")
            .IsUnique()
            .HasFilter("[IsImplicit] = 1");
        builder.HasIndex(s => s.DefaultWorkflowId);

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Key).ValueGeneratedOnAdd();

        builder.Property(s => s.OwnerType).IsRequired().HasColumnType("varchar").HasMaxLength(64);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(128);
        builder.Property(s => s.Description).HasMaxLength(1024);

        builder.Property(s => s.Ownership).IsRequired()
            .HasConversion<EnumConverter<WorkflowOwnership>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(s => s.ExternalKey).HasMaxLength(256);
        builder.Property(s => s.IsImplicit).IsRequired();
        builder.Property(s => s.DefaultWorkflowId).IsRequired();

        builder.HasOne<StatusWorkflow>()
            .WithMany()
            .HasForeignKey(s => s.DefaultWorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Entries)
            .WithOne()
            .HasForeignKey(e => e.SchemeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Entries).HasField("_entries").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class StatusWorkflowSchemeEntryConfiguration : IEntityTypeConfiguration<StatusWorkflowSchemeEntry>
{
    public void Configure(EntityTypeBuilder<StatusWorkflowSchemeEntry> builder)
    {
        builder.ToTable("WorkflowSchemeEntries", SchemaNames.StatusWorkflows);

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.SchemeId, e.RecordTypeId }).IsUnique();
        builder.HasIndex(e => e.WorkflowId);
        builder.HasIndex(e => e.RecordTypeId);

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.SchemeId).IsRequired();
        builder.Property(e => e.RecordTypeId).IsRequired();
        builder.Property(e => e.WorkflowId).IsRequired();

        builder.HasOne<RecordType>()
            .WithMany()
            .HasForeignKey(e => e.RecordTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StatusWorkflow>()
            .WithMany()
            .HasForeignKey(e => e.WorkflowId)
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
