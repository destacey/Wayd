using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Attachments;
using Wayd.Common.Domain.Following;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Maps <see cref="Attachment"/> to <c>App.Attachments</c>: who each image belongs to, without its bytes.
/// </summary>
public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", SchemaNames.App);

        // Version 7 ids increase with time, so inserts append to the clustered index.
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // A record's images, which every save of its markdown reads to find the ones it dropped.
        builder.HasIndex(a => new { a.OwnerType, a.OwnerId });

        // The sweep of uploads no save claimed.
        builder.HasIndex(a => a.UploadedOn)
            .HasFilter("[OwnerId] IS NULL")
            .HasDatabaseName("IX_Attachments_Pending");

        builder.Property(a => a.ContentType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(a => a.Size).IsRequired();
        builder.Property(a => a.Width).IsRequired();
        builder.Property(a => a.Height).IsRequired();

        builder.Property(a => a.Sha256).IsRequired()
            .HasColumnType("char(64)");

        builder.Property(a => a.UploadedBy).IsRequired()
            .HasMaxLength(Attachment.UserIdMaxLength);

        builder.Property(a => a.UploadedOn).IsRequired();

        // No foreign key, as for follows: the owner lives in a different table per owner type.
        builder.Property(a => a.OwnerType)
            .HasColumnType("varchar")
            .HasMaxLength(FollowOwnerDescriptor.KeyMaxLength);
        // A concurrency token, so that access can never move: two saves claiming one upload, or the sweep deleting
        // one a save has just claimed, conflict instead of the later silently winning.
        builder.Property(a => a.OwnerId).IsConcurrencyToken();
        builder.Property(a => a.ClaimedOn);

        builder.Ignore(a => a.IsPending);
    }
}

/// <summary>
/// Maps <see cref="AttachmentContent"/> to <c>App.AttachmentContents</c>: one row of bytes per attachment.
/// </summary>
public class AttachmentContentConfiguration : IEntityTypeConfiguration<AttachmentContent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AttachmentContent> builder)
    {
        builder.ToTable("AttachmentContents", SchemaNames.App);

        builder.HasKey(c => c.AttachmentId);
        builder.Property(c => c.AttachmentId).ValueGeneratedNever();

        builder.Property(c => c.Data).IsRequired()
            .HasColumnType("varbinary(max)");

        // Cascade so that no path that deletes an attachment can leave its bytes behind.
        builder.HasOne<Attachment>()
            .WithOne()
            .HasForeignKey<AttachmentContent>(c => c.AttachmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
