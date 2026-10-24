using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Following;

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
