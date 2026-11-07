using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>Maps <see cref="FeatureFlag"/> to <c>FeatureManagement.FeatureFlags</c>, unique by name.</summary>
public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable(name: "FeatureFlags", SchemaNames.FeatureManagement);

        builder.HasKey(f => f.Id);

        builder.HasIndex(f => f.Name)
            .IsUnique();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(f => f.DisplayName)
            .IsRequired()
            .HasMaxLength(FeatureFlag.DisplayNameMaxLength);

        builder.Property(f => f.Description)
            .HasMaxLength(FeatureFlag.DescriptionMaxLength);

        builder.Property(f => f.IsEnabled)
            .IsRequired();

        builder.Property(f => f.IsArchived)
            .IsRequired();

        builder.Property(f => f.Module)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(f => f.IsModuleSwitch)
            .IsRequired();

        builder.Property(f => f.FiltersJson)
            .HasColumnType("nvarchar(max)");
    }
}
