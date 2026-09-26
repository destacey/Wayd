using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class SystemSettingsSectionConfiguration : IEntityTypeConfiguration<SystemSettingsSection>
{
    public void Configure(EntityTypeBuilder<SystemSettingsSection> builder)
    {
        builder.ToTable("SystemSettings", SchemaNames.App);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasIndex(s => new { s.Scope, s.Key })
            .IsUnique();

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.Scope)
            .IsRequired()
            .HasConversion<EnumConverter<SettingsScope>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.SchemaVersion)
            .IsRequired();

        // Two saves of a section read it, change it and write it back; the second to commit must fail
        // rather than overwrite the first and record a Previous that was never in effect.
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();
    }
}
