using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    public void Configure(EntityTypeBuilder<ProductType> builder)
    {
        builder.ToTable("ProductTypes", SchemaNames.ProductManagement);

        builder.HasKey(t => t.Id);
        builder.HasAlternateKey(t => t.Key);

        builder.HasIndex(t => t.Name).IsUnique();

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Key).ValueGeneratedOnAdd();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Description).HasMaxLength(512);
        builder.Property(t => t.IsReleasable).IsRequired();
        builder.Property(t => t.Order).IsRequired();
        builder.Property(t => t.IsSystem).IsRequired();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", SchemaNames.ProductManagement);

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => p.Key);

        builder.HasIndex(p => p.ParentId);
        builder.HasIndex(p => p.ProductTypeId);
        builder.HasIndex(p => p.StatusCategory)
            .IncludeProperties(p => new { p.Id, p.Key, p.Name, p.ParentId, p.ProductTypeId });

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).ValueGeneratedOnAdd();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Description).HasMaxLength(1024);
        builder.Property(p => p.ProductTypeId).IsRequired();
        builder.Property(p => p.ParentId);
        builder.Property(p => p.ExternalId).HasMaxLength(256);

        builder.Property(p => p.StatusId).IsRequired();
        builder.Property(p => p.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(p => p.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<int>("StatusAliasValue").IsRequired();
        builder.Property(p => p.StatusTransitionCount).IsRequired();

        // The history is written through the aggregate but queried on its own, so it is a plain
        // collection rather than a navigation every read would have to include.
        builder.Ignore(p => p.StatusTransitions);

        // Ignore
        builder.Ignore(p => p.StatusAlias);

        // Relationships
        builder.HasOne<ProductType>()
            .WithMany()
            .HasForeignKey(p => p.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing: restrict rather than cascade so removing a node cannot silently take its
        // subtree with it. Product.Remove refuses while children exist.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.ToTable("Releases", SchemaNames.Delivery);

        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => r.Key);

        builder.HasIndex(r => new { r.ProductId, r.ReleasedDate })
            .IncludeProperties(r => new { r.Id, r.Key, r.Version, r.Name, r.Sequence, r.StatusCategory });
        builder.HasIndex(r => r.PackageId);

        // Deliberately NOT unique: duplicate versions within a product warn rather than block, since an
        // importer with a mis-set truncation rule is the case this diagnoses.
        builder.HasIndex(r => new { r.ProductId, r.Version });

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();

        builder.Property(r => r.ProductId).IsRequired();

        // Free text, never parsed. Nothing sorts or compares on this column.
        builder.Property(r => r.Version).IsRequired().HasMaxLength(128);

        builder.Property(r => r.Name).HasMaxLength(256);
        builder.Property(r => r.Sequence);
        builder.Property(r => r.TargetDate);
        builder.Property(r => r.CutDate);
        builder.Property(r => r.ReleasedDate);
        builder.Property(r => r.Notes).HasMaxLength(4000);
        builder.Property(r => r.PackageId);

        builder.Property(r => r.StatusId).IsRequired();
        builder.Property(r => r.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(r => r.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<int>("StatusAliasValue").IsRequired();
        builder.Property(r => r.StatusTransitionCount).IsRequired();

        // The history is written through the aggregate but queried on its own, so it is a plain
        // collection rather than a navigation every read would have to include.
        builder.Ignore(r => r.StatusTransitions);

        // Relationships
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ReleasePackage>()
            .WithMany()
            .HasForeignKey(r => r.PackageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ReleasePackageConfiguration : IEntityTypeConfiguration<ReleasePackage>
{
    public void Configure(EntityTypeBuilder<ReleasePackage> builder)
    {
        builder.ToTable("ReleasePackages", SchemaNames.Delivery);

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => p.Key);

        builder.HasIndex(p => p.ReleasedDate)
            .IncludeProperties(p => new { p.Id, p.Key, p.Version, p.Name, p.StatusCategory });

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).ValueGeneratedOnAdd();

        builder.Property(p => p.Version).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Name).HasMaxLength(256);
        builder.Property(p => p.TargetDate);
        builder.Property(p => p.ReleasedDate);

        builder.Property(p => p.StatusId).IsRequired();
        builder.Property(p => p.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(p => p.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<int>("StatusAliasValue").IsRequired();
        builder.Property(p => p.StatusTransitionCount).IsRequired();

        // The history is written through the aggregate but queried on its own, so it is a plain
        // collection rather than a navigation every read would have to include.
        builder.Ignore(p => p.StatusTransitions);

        // Relationships
        builder.HasMany(p => p.Components)
            .WithOne()
            .HasForeignKey(c => c.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Components).HasField("_components").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ignore
        builder.Ignore(p => p.ChangedComponents);
    }
}

public class ReleasePackageComponentConfiguration : IEntityTypeConfiguration<ReleasePackageComponent>
{
    public void Configure(EntityTypeBuilder<ReleasePackageComponent> builder)
    {
        builder.ToTable("ReleasePackageComponents", SchemaNames.Delivery);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.PackageId, c.ProductId }).IsUnique();
        builder.HasIndex(c => c.ProductId);

        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.PackageId).IsRequired();
        builder.Property(c => c.ProductId).IsRequired();
        builder.Property(c => c.ReleaseId);
        builder.Property(c => c.Version).IsRequired().HasMaxLength(128);

        builder.Property(c => c.Kind).IsRequired()
            .HasConversion<EnumConverter<ManifestEntryKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // Relationships
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // No FK to Release: a carried-forward component often has no release row in Wayd at all, and the
        // manifest has to keep answering "what was running" regardless.
    }
}

public class DeploymentEnvironmentConfiguration : IEntityTypeConfiguration<DeploymentEnvironment>
{
    public void Configure(EntityTypeBuilder<DeploymentEnvironment> builder)
    {
        builder.ToTable("DeploymentEnvironments", SchemaNames.Delivery);

        builder.HasKey(e => e.Id);
        builder.HasAlternateKey(e => e.Key);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => new { e.Category, e.IsActive });

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Key).ValueGeneratedOnAdd();

        builder.Property(e => e.Name).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Category).IsRequired()
            .HasConversion<EnumConverter<EnvironmentCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(e => e.RingOrder).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
    }
}

public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("Deployments", SchemaNames.Delivery);

        builder.HasKey(d => d.Id);
        builder.HasAlternateKey(d => d.Key);

        // The delivery-metrics index: deployment frequency, change failure rate and time-to-restore all
        // filter production deployments by outcome over a window. Outcome being int keeps this narrow.
        builder.HasIndex(d => new { d.EnvironmentCategory, d.Outcome, d.CompletedAt })
            .IncludeProperties(d => new { d.Id, d.Key, d.ReleaseId, d.PackageId, d.EnvironmentId });

        builder.HasIndex(d => d.ReleaseId);
        builder.HasIndex(d => d.PackageId);
        builder.HasIndex(d => new { d.EnvironmentId, d.StartedAt });

        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Key).ValueGeneratedOnAdd();

        builder.Property(d => d.ReleaseId);
        builder.Property(d => d.PackageId);
        builder.Property(d => d.EnvironmentId).IsRequired();

        // Frozen at deployment time so reclassifying an environment cannot rewrite what past
        // deployments counted as.
        builder.Property(d => d.EnvironmentCategory).IsRequired()
            .HasConversion<EnumConverter<EnvironmentCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(d => d.ArtifactId).HasMaxLength(128);
        builder.Property(d => d.StartedAt).IsRequired();
        builder.Property(d => d.CompletedAt);
        builder.Property(d => d.Reason).HasMaxLength(1024);

        builder.Property(d => d.StatusId).IsRequired();
        builder.Property(d => d.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(d => d.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property<int>("StatusAliasValue").IsRequired();
        builder.Property(d => d.StatusTransitionCount).IsRequired();

        // The history is written through the aggregate but queried on its own, so it is a plain
        // collection rather than a navigation every read would have to include.
        builder.Ignore(d => d.StatusTransitions);

        // Relationships
        builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey(d => d.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ReleasePackage>()
            .WithMany()
            .HasForeignKey(d => d.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DeploymentEnvironment>()
            .WithMany()
            .HasForeignKey(d => d.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore
        builder.Ignore(d => d.Outcome);
        builder.Ignore(d => d.IsComplete);
        builder.Ignore(d => d.IsChangeFailure);
    }
}
