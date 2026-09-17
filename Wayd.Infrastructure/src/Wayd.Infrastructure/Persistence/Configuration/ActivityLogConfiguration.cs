using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Activities;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        builder.ToTable("ActivityLogs", SchemaNames.App);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EventType).IsRequired().HasColumnType("varchar").HasMaxLength(128);
        builder.Property(x => x.Category)
            .IsRequired()
            .HasConversion<EnumConverter<ActivityCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(x => x.EventVersion).IsRequired().HasColumnType("varchar").HasMaxLength(16).HasDefaultValue("1.0");
        builder.Property(x => x.DomainArea).IsRequired().HasColumnType("varchar").HasMaxLength(64);
        builder.Property(x => x.AggregateType).IsRequired().HasColumnType("varchar").HasMaxLength(64);
        builder.Property(x => x.AggregateId).IsRequired();

        builder.Property(x => x.ActorKind)
            .IsRequired()
            .HasConversion<EnumConverter<EventActorKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.EmployeeId);
        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.Timestamp).IsRequired();
        builder.Property(x => x.Ordinal).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(512);

        // A plain relationship rather than an owned collection: EF loads an owned collection with every entry,
        // which would join this table into every activity read, and fail any query of the log run against a
        // schema that predates the table — the migration tests do exactly that.
        builder.HasMany(x => x.RelatedAggregates)
            .WithOne()
            .HasForeignKey("ActivityLogId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.RelatedAggregates)
            .HasField("_relatedAggregates")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes for querying activity. Each ends in Ordinal because reads sort by Timestamp then Ordinal,
        // and the primary key lands on the end of a non-unique index anyway, so the whole sort is covered.
        //
        // AggregateId leads the record index, ahead of AggregateType: a read always filters on the id and only
        // optionally on the type, so leading with the type would force a scan across every type whenever it is
        // omitted. The id is a Guid and the type one of a dozen strings, so it is also the selective half.
        builder.HasIndex(x => new { x.AggregateId, x.AggregateType, x.Timestamp, x.Ordinal });
        builder.HasIndex(x => new { x.Timestamp, x.Ordinal });
        builder.HasIndex(x => new { x.UserId, x.Timestamp, x.Ordinal });
        builder.HasIndex(x => x.CorrelationId);
    }
}

public class ActivityLogRelatedAggregateConfiguration : IEntityTypeConfiguration<ActivityLogRelatedAggregate>
{
    public void Configure(EntityTypeBuilder<ActivityLogRelatedAggregate> builder)
    {
        builder.ToTable("ActivityLogRelatedAggregates", SchemaNames.App);

        builder.Property<Guid>("ActivityLogId");
        builder.HasKey("ActivityLogId", nameof(ActivityLogRelatedAggregate.AggregateType), nameof(ActivityLogRelatedAggregate.AggregateId));

        builder.Property(x => x.AggregateType).IsRequired().HasColumnType("varchar").HasMaxLength(64);
        builder.Property(x => x.AggregateId).IsRequired();

        // Led by the id for the same reason as the entry's own record index: a read always has the id.
        builder.HasIndex(x => new { x.AggregateId, x.AggregateType });
    }
}

