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
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(512);

        // Indexes for querying activity
        builder.HasIndex(x => new { x.AggregateType, x.AggregateId, x.Timestamp });
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => new { x.UserId, x.Timestamp });
        builder.HasIndex(x => x.CorrelationId);
    }
}

