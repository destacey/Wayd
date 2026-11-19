using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Maps <see cref="Kpi"/> to <c>Kpis.Kpis</c>: one row per KPI, whatever record it is on.
/// </summary>
public class KpiConfiguration : IEntityTypeConfiguration<Kpi>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Kpi> builder)
    {
        builder.ToTable("Kpis", SchemaNames.Kpis);

        builder.HasKey(k => k.Id);
        builder.HasAlternateKey(k => k.Key);

        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.Key).ValueGeneratedOnAdd();

        // A scorecard reads one subject's KPIs in order, open ones only.
        builder.HasIndex(k => new { k.SubjectType, k.SubjectId, k.Status, k.Order });

        builder.HasIndex(k => k.CategoryId);

        builder.Property(k => k.SubjectType).IsRequired()
            .HasColumnType("varchar")
            .HasMaxLength(KpiSubjectTypeDescriptor.KeyMaxLength);

        // No foreign key: one table serves every subject type, so the record lives in a different table per module.
        builder.Property(k => k.SubjectId).IsRequired();

        builder.Property(k => k.Name).IsRequired().HasMaxLength(Kpi.NameMaxLength);
        builder.Property(k => k.Description).HasMaxLength(Kpi.DescriptionMaxLength);
        builder.Property(k => k.Prefix).HasMaxLength(Kpi.AffixMaxLength);
        builder.Property(k => k.Suffix).HasMaxLength(Kpi.AffixMaxLength);

        builder.Property(k => k.Direction).IsRequired()
            .HasConversion<EnumConverter<KpiTargetDirection>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property<double?>("_baselineValue").HasColumnName("BaselineValue");
        builder.Property<LocalDate?>("_baselineWindowStart").HasColumnName("BaselineWindowStart");
        builder.Property<LocalDate?>("_baselineWindowEnd").HasColumnName("BaselineWindowEnd");

        builder.Property<KpiTargetKind>("_targetKind").HasColumnName("TargetKind").IsRequired()
            .HasConversion<EnumConverter<KpiTargetKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<double>("_targetValue").HasColumnName("TargetValue").IsRequired();

        builder.Property<KpiAtRiskKind?>("_atRiskKind").HasColumnName("AtRiskKind")
            .HasConversion<EnumConverter<KpiAtRiskKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<double?>("_atRiskValue").HasColumnName("AtRiskValue");

        builder.Property<KpiWindowKind>("_windowKind").HasColumnName("WindowKind").IsRequired()
            .HasConversion<EnumConverter<KpiWindowKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<LocalDate?>("_windowAnchorDate").HasColumnName("WindowAnchorDate");
        builder.Property<int?>("_windowRollingCount").HasColumnName("WindowRollingCount");
        builder.Property<KpiRollingUnit?>("_windowRollingUnit").HasColumnName("WindowRollingUnit")
            .HasConversion<EnumConverter<KpiRollingUnit>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(k => k.RollingAverageOf);

        builder.Ignore(k => k.Baseline);
        builder.Ignore(k => k.Target);
        builder.Ignore(k => k.AtRisk);
        builder.Ignore(k => k.Goal);
        builder.Ignore(k => k.Details);
        builder.Ignore(k => k.Window);

        builder.Property(k => k.Source).IsRequired()
            .HasConversion<EnumConverter<KpiSource>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(k => k.ExpressedAs).IsRequired()
            .HasConversion<EnumConverter<KpiExpressedAs>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(k => k.Status).IsRequired()
            .HasConversion<EnumConverter<KpiStatus>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(k => k.Order).IsRequired();

        // Two people changing one KPI at once collide here rather than the second silently undoing the first. A
        // measurement change touches the KPI's row too (IKpisDbContext.TouchKpi), so two clashing measurements recorded
        // at once collide here as well.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // Restrict: a category KPIs use is deactivated, never deleted.
        builder.HasOne<KpiCategory>()
            .WithMany()
            .HasForeignKey(k => k.CategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(k => k.Measurements)
            .WithOne()
            .HasForeignKey(m => m.KpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(k => k.Measurements).HasField("_measurements").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Maps <see cref="KpiMeasurement"/> to <c>Kpis.Measurements</c>: the raw value of one KPI for one period.
/// </summary>
public class KpiMeasurementConfiguration : IEntityTypeConfiguration<KpiMeasurement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KpiMeasurement> builder)
    {
        builder.ToTable("Measurements", SchemaNames.Kpis);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Ignore(m => m.Period);
        builder.Property(m => m.PeriodStart).IsRequired();
        builder.Property(m => m.PeriodEnd).IsRequired();

        // Serves two things. A scorecard reads each KPI's newest values by period end, then moment. And it is a partial
        // backstop for the domain's clash rules, which two concurrent saves could each pass: no two measurements on a
        // KPI end on the same day unless they are point-in-time readings at different moments. Two per-period
        // measurements that overlap from different end days still need the domain. No filter: SQL Server then counts
        // null moments as equal, which is what makes the rule hold for every window but point in time.
        builder.HasIndex(m => new { m.KpiId, m.PeriodEnd, m.MeasuredAt })
            .IsUnique()
            .HasFilter(null)
            .IsDescending(false, true, true)
            .IncludeProperties(m => m.Value);

        builder.Property(m => m.Value).IsRequired();

        builder.Property(m => m.MeasuredAt);

        builder.Property(m => m.PeriodRecordKind)
            .HasConversion<EnumConverter<KpiPeriodRecordKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // No foreign key: it points at a sprint, planning interval or release, each in its own module's table.
        builder.Property(m => m.PeriodRecordId);

        builder.Property(m => m.MeasuredOn).IsRequired();

        builder.Property(m => m.Source).IsRequired()
            .HasConversion<EnumConverter<KpiMeasurementSource>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(m => m.SampleSize);
        builder.Property(m => m.Note).HasMaxLength(KpiMeasurement.NoteMaxLength);

        // No action: employees are only ever soft-deleted, so the constraint never has a row to act on.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.MeasuredById)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>
/// Maps <see cref="KpiCategory"/> to <c>Kpis.Categories</c>: the organization's groupings for KPIs.
/// </summary>
public class KpiCategoryConfiguration : IEntityTypeConfiguration<KpiCategory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KpiCategory> builder)
    {
        builder.ToTable("Categories", SchemaNames.Kpis);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // Two categories cannot share a name; the default collation makes it case-insensitive.
        builder.HasIndex(c => c.Name).IsUnique();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(KpiCategory.NameMaxLength);
        builder.Property(c => c.Description).HasMaxLength(KpiCategory.DescriptionMaxLength);
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
    }
}
