using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ScoringModelConfiguration : IEntityTypeConfiguration<ScoringModel>
{
    public void Configure(EntityTypeBuilder<ScoringModel> builder)
    {
        builder.ToTable("ScoringModels", SchemaNames.App);

        builder.HasKey(m => m.Id);
        builder.HasAlternateKey(m => m.Key);

        builder.HasIndex(m => m.State);

        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Key).ValueGeneratedOnAdd();
        builder.Property(m => m.Name).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(1024).IsRequired();

        builder.Property(m => m.State).IsRequired()
            .HasConversion<EnumConverter<ScoringModelState>>()
            .HasMaxLength(32)
            .HasColumnType("varchar");

        // Relationships
        builder.HasMany(m => m.Criteria)
            .WithOne()
            .HasForeignKey(c => c.ScoringModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Scales)
            .WithOne()
            .HasForeignKey(s => s.ScoringModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Outputs)
            .WithOne()
            .HasForeignKey(o => o.ScoringModelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ScoringModelCriterionConfiguration : IEntityTypeConfiguration<ScoringModelCriterion>
{
    public void Configure(EntityTypeBuilder<ScoringModelCriterion> builder)
    {
        builder.ToTable("ScoringModelCriteria", SchemaNames.App);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.ScoringModelId);

        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(128).IsRequired();
        builder.Property(c => c.Token).HasMaxLength(32).HasColumnType("varchar").IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1024);
        builder.Property(c => c.Weight).HasColumnType("decimal(5,2)");
        builder.Property(c => c.ScaleId);
        builder.Property(c => c.Order).IsRequired();

        builder.HasIndex(c => c.ScaleId);

        // A criterion may reference a scale; the aggregate guards removal of referenced scales.
        builder.HasOne<ScoringScale>()
            .WithMany()
            .HasForeignKey(c => c.ScaleId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ScoringScaleConfiguration : IEntityTypeConfiguration<ScoringScale>
{
    public void Configure(EntityTypeBuilder<ScoringScale> builder)
    {
        builder.ToTable("ScoringScales", SchemaNames.App);

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.ScoringModelId);

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Order).IsRequired();

        builder.HasMany(s => s.Levels)
            .WithOne()
            .HasForeignKey(l => l.ScoringScaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ScoringRatingLevelConfiguration : IEntityTypeConfiguration<ScoringRatingLevel>
{
    public void Configure(EntityTypeBuilder<ScoringRatingLevel> builder)
    {
        builder.ToTable("ScoringRatingLevels", SchemaNames.App);

        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.ScoringScaleId);

        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Label).HasMaxLength(64).IsRequired();
        builder.Property(l => l.Value).HasColumnType("decimal(9,4)").IsRequired();
        builder.Property(l => l.Order).IsRequired();
    }
}

public class ScoringModelOutputConfiguration : IEntityTypeConfiguration<ScoringModelOutput>
{
    public void Configure(EntityTypeBuilder<ScoringModelOutput> builder)
    {
        builder.ToTable("ScoringModelOutputs", SchemaNames.App);

        builder.HasKey(o => o.Id);

        builder.HasIndex(o => o.ScoringModelId);

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(128).IsRequired();
        builder.Property(o => o.Token).HasMaxLength(32).HasColumnType("varchar").IsRequired();
        builder.Property(o => o.Formula).HasMaxLength(1024).IsRequired();
        builder.Property(o => o.IsPrimary).IsRequired();
        builder.Property(o => o.Order).IsRequired();
    }
}
