using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Employees;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Maps <see cref="DiscussionChannel"/> to <c>Discussions.Channels</c>: one row per record's conversation.
/// </summary>
public class DiscussionChannelConfiguration : IEntityTypeConfiguration<DiscussionChannel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DiscussionChannel> builder)
    {
        builder.ToTable("Channels", SchemaNames.Discussions);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // One conversation per record: two first posts at once collide here rather than opening two. Filtered so
        // the conversations with no record that a chat area adds later are not all one duplicate.
        builder.HasIndex(c => new { c.ChannelType, c.OwnerId })
            .IsUnique()
            .HasFilter("[OwnerId] IS NOT NULL");

        builder.Property(c => c.ChannelType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(DiscussionChannelTypeDescriptor.KeyMaxLength);

        // No foreign key: one table serves every channel type, so the record lives in a different table per module.
        builder.Property(c => c.OwnerId);

        builder.Property(c => c.CreatedOn).IsRequired();
        builder.Property(c => c.LockedOn);

        builder.Ignore(c => c.IsLocked);

        // Two moderators locking and unlocking at once, or a lock racing the record's deletion, collide here rather
        // than the second silently undoing the first.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // No action: employees are only ever soft-deleted, so the constraint never has a row to act on.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(c => c.LockedById)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>
/// Maps <see cref="DiscussionMessage"/> to <c>Discussions.Messages</c>: top-level messages and replies alike.
/// </summary>
public class DiscussionMessageConfiguration : IEntityTypeConfiguration<DiscussionMessage>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DiscussionMessage> builder)
    {
        builder.ToTable("Messages", SchemaNames.Discussions);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        // Both reads of a conversation in posting order: its top-level messages (parent null), and one thread's
        // replies. Kind is carried so the filter on what the caller may read needs no lookup.
        builder.HasIndex(m => new { m.ChannelId, m.ParentMessageId, m.PostedOn })
            .IncludeProperties(m => m.Kind);

        builder.HasIndex(m => m.AuthorEmployeeId);

        builder.Property(m => m.Kind)
            .IsRequired()
            .HasConversion<EnumConverter<DiscussionMessageKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(m => m.Subject).HasMaxLength(DiscussionMessage.SubjectMaxLength);

        // Longer than nvarchar's 4,000-character limit; the domain caps it at 10,000.
        builder.Property(m => m.Body).HasColumnType("nvarchar(max)");

        builder.Property(m => m.PostedOn).IsRequired();
        builder.Property(m => m.EditedOn);
        builder.Property(m => m.DeletedOn);

        builder.Ignore(m => m.IsDeleted);
        builder.Ignore(m => m.IsTopLevel);

        // An edit and a delete of one message at once collide here. Without it, whichever saved second wins: an edit
        // after a delete puts text back on the tombstone, and a delete after an edit leaves the edit's revision behind.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasOne<DiscussionChannel>()
            .WithMany()
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // No action: SQL Server refuses a cascade cycle on one table. Replies leave only with their channel, whose
        // cascade removes a thread and its parent in the same statement.
        builder.HasOne<DiscussionMessage>()
            .WithMany()
            .HasForeignKey(m => m.ParentMessageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.AuthorEmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.DeletedById)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(m => m.Revisions)
            .WithOne()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Revisions).HasField("_revisions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Mentions)
            .WithOne()
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Mentions).HasField("_mentions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Maps <see cref="DiscussionMessageRevision"/> to <c>Discussions.MessageRevisions</c>: every version an edit replaced.
/// </summary>
public class DiscussionMessageRevisionConfiguration : IEntityTypeConfiguration<DiscussionMessageRevision>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DiscussionMessageRevision> builder)
    {
        builder.ToTable("MessageRevisions", SchemaNames.Discussions);

        // An increasing integer rather than a GUID, so inserts append to the clustered index.
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityColumn();

        builder.HasIndex(r => r.MessageId);

        builder.Property(r => r.Subject).HasMaxLength(DiscussionMessage.SubjectMaxLength);
        builder.Property(r => r.Body).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(r => r.ReplacedOn).IsRequired();
    }
}

/// <summary>
/// Maps <see cref="DiscussionMention"/> to <c>Discussions.Mentions</c>: one row per message per person it mentions.
/// </summary>
public class DiscussionMentionConfiguration : IEntityTypeConfiguration<DiscussionMention>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DiscussionMention> builder)
    {
        builder.ToTable("Mentions", SchemaNames.Discussions);

        builder.HasKey(m => new { m.MessageId, m.EmployeeId });

        // A later "mentions of me" list, newest first, without a schema change.
        builder.HasIndex(m => new { m.EmployeeId, m.MentionedOn });

        builder.Property(m => m.MentionedOn).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
