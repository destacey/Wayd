using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Maps <see cref="Follow"/> to <c>Notifications.Follows</c>: one row per person per followed record.
/// </summary>
public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("Follows", SchemaNames.Notifications);

        // Record first, so the clustered key also answers "who follows this record", which every
        // notification for that record will ask. The key's uniqueness is what makes following twice a
        // no-op rather than a second row.
        builder.HasKey(f => new { f.OwnerType, f.OwnerId, f.EmployeeId });

        // The Following list: one person's follows of one owner type, newest first.
        builder.HasIndex(f => new { f.EmployeeId, f.OwnerType, f.FollowedOn })
            .IncludeProperties(f => f.OwnerId);

        builder.Property(f => f.OwnerType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(FollowOwnerDescriptor.KeyMaxLength);

        // No foreign key: one table serves every owner type, so the target lives in a different table
        // per module.
        builder.Property(f => f.OwnerId).IsRequired();

        builder.Property(f => f.FollowedOn).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(f => f.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Maps <see cref="RecordMute"/> to <c>Notifications.RecordMutes</c>: one row per person per muted record.
/// </summary>
public class RecordMuteConfiguration : IEntityTypeConfiguration<RecordMute>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordMute> builder)
    {
        builder.ToTable("RecordMutes", SchemaNames.Notifications);

        // Record first, as for follows: writing a notification asks who muted this record.
        builder.HasKey(m => new { m.OwnerType, m.OwnerId, m.EmployeeId });

        // The list of everything a person has muted, newest first.
        builder.HasIndex(m => new { m.EmployeeId, m.MutedOn })
            .IncludeProperties(m => new { m.OwnerType, m.OwnerId });

        builder.Property(m => m.OwnerType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(FollowOwnerDescriptor.KeyMaxLength);

        // No foreign key, as for follows: the target lives in a different table per owner type.
        builder.Property(m => m.OwnerId).IsRequired();

        builder.Property(m => m.MutedOn).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Maps <see cref="ReasonMute"/> to <c>Notifications.ReasonMutes</c>: one row per person per muted reason.
/// </summary>
public class ReasonMuteConfiguration : IEntityTypeConfiguration<ReasonMute>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReasonMute> builder)
    {
        builder.ToTable("ReasonMutes", SchemaNames.Notifications);

        builder.HasKey(m => new { m.EmployeeId, m.Reason });

        // The reason's flag value, as notifications store their reasons; the enum is never renumbered.
        builder.Property(m => m.Reason).IsRequired();

        builder.Property(m => m.MutedOn).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Maps <see cref="Notification"/> to <c>Notifications.Notifications</c>: one row per person per event.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", SchemaNames.Notifications);

        // An increasing integer rather than a GUID, so inserts append to the clustered index.
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).UseIdentityColumn();

        // One person, one notification per event: a redelivered event collides here instead of
        // writing a second row.
        builder.HasIndex(n => new { n.EventId, n.RecipientEmployeeId }).IsUnique();

        // The list: one person's notifications grouped by record, with everything the grouping counts.
        builder.HasIndex(n => new { n.RecipientEmployeeId, n.OwnerType, n.OwnerId, n.OccurredOn })
            .IncludeProperties(n => new { n.ReadOn, n.Reasons });

        // The unread count and "mark all read", which only ever touch unread rows.
        builder.HasIndex(n => new { n.RecipientEmployeeId, n.OwnerType, n.OwnerId })
            .HasFilter("[ReadOn] IS NULL")
            .HasDatabaseName("IX_Notifications_Unread");

        // The retention sweep.
        builder.HasIndex(n => n.OccurredOn);

        builder.Property(n => n.OwnerType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(FollowOwnerDescriptor.KeyMaxLength);

        // No foreign key, as for follows: the target lives in a different table per owner type.
        builder.Property(n => n.OwnerId).IsRequired();

        builder.Property(n => n.EventId).IsRequired();

        builder.Property(n => n.Event)
            .IsRequired()
            .HasConversion<EnumConverter<NotificationEvent>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(n => n.ActorKind)
            .IsRequired()
            .HasConversion<EnumConverter<EventActorKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(n => n.OccurredOn).IsRequired();
        builder.Property(n => n.Reasons).IsRequired();
        builder.Property(n => n.ReadOn);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(n => n.RecipientEmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // No action: SQL Server refuses a second cascading path from Employees, and employees are only
        // ever soft-deleted, so the constraint never has a row to act on.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(n => n.ActorEmployeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
