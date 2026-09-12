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

