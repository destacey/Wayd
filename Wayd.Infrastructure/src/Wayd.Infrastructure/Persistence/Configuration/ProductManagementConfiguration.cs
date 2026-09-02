using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

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
        builder.Property(t => t.IsActive).IsRequired();
    }
}

public class ProductTagCategoryConfiguration : IEntityTypeConfiguration<ProductTagCategory>
{
    public void Configure(EntityTypeBuilder<ProductTagCategory> builder)
    {
        builder.ToTable("ProductTagCategories", SchemaNames.ProductManagement);

        builder.HasKey(c => c.Id);
        builder.HasAlternateKey(c => c.Key);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Key).ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Description).HasMaxLength(512);
        builder.Property(c => c.AllowsMany).IsRequired();
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.IsSystem).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasMany(c => c.Tags)
            .WithOne()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Tags).HasField("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> builder)
    {
        builder.ToTable("ProductTags", SchemaNames.ProductManagement);

        builder.HasKey(t => t.Id);

        // Unique per axis, not globally: "ios" on Platform and "ios" on Tech Stack are different tags.
        builder.HasIndex(t => new { t.CategoryId, t.Name }).IsUnique();

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.CategoryId).IsRequired();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Description).HasMaxLength(512);
        builder.Property(t => t.IsActive).IsRequired();
    }
}

public class ProductTagAssignmentConfiguration : IEntityTypeConfiguration<ProductTagAssignment>
{
    public void Configure(EntityTypeBuilder<ProductTagAssignment> builder)
    {
        builder.ToTable("ProductTagAssignments", SchemaNames.ProductManagement);

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.ProductId, a.TagId }).IsUnique();

        // "Every product tagged ios", and "every product with a Platform tag" — the reason the
        // category is denormalized onto the assignment.
        builder.HasIndex(a => a.TagId);
        builder.HasIndex(a => new { a.CategoryId, a.TagId }).IncludeProperties(a => a.ProductId);

        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ProductId).IsRequired();
        builder.Property(a => a.TagId).IsRequired();
        builder.Property(a => a.CategoryId).IsRequired();

        builder.HasOne(a => a.Tag)
            .WithMany()
            .HasForeignKey(a => a.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        // No relationship for CategoryId: it is denormalized off the tag so filtering by axis needs no
        // join, and carries no foreign key. A projection reaches the category through the tag.
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
        builder.Property(p => p.StatusWorkflowId).IsRequired();
        // Indexed for the reassignment migrator, whose only query is "every record on
        // this workflow". Unindexed it scans the whole table on every batch.
        builder.HasIndex(p => p.StatusWorkflowId);
        builder.Property(p => p.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(p => p.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(e => e.StatusAliasValue).IsRequired();
        builder.Property(p => p.StatusTransitionCount).IsRequired();

        builder.ConfigureStatusHistory();

        builder.HasMany(p => p.Tags)
            .WithOne()
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Tags).HasField("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ignore
        builder.Ignore(p => p.StatusAlias);

        // Relationships
        builder.HasOne(p => p.ProductType)
            .WithMany()
            .HasForeignKey(p => p.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing: restrict rather than cascade so removing a node cannot silently take its
        // subtree with it. Product.Remove refuses while children exist.
        builder.HasOne(p => p.Parent)
            .WithMany()
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VersionConfiguration : IEntityTypeConfiguration<Version>
{
    public void Configure(EntityTypeBuilder<Version> builder)
    {
        builder.ToTable("Versions", SchemaNames.Delivery);

        builder.HasKey(v => v.Id);
        builder.HasAlternateKey(v => v.Key);

        builder.HasIndex(v => new { v.ProductId, v.ReleasedDate })
            .IncludeProperties(v => new { v.Id, v.Key, v.Number, v.Name, v.Sequence, v.StatusCategory });

        // Deliberately NOT unique: duplicate version numbers within a product warn rather than block,
        // since an importer with a mis-set truncation rule is the case this diagnoses.
        builder.HasIndex(v => new { v.ProductId, v.Number });

        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Key).ValueGeneratedOnAdd();

        builder.Property(v => v.ProductId).IsRequired();

        // Free text, never parsed. Nothing sorts or compares on this column.
        builder.Property(v => v.Number).IsRequired().HasMaxLength(128);

        builder.Property(v => v.Name).HasMaxLength(256);
        builder.Property(v => v.Sequence);
        builder.Property(v => v.TargetDate);
        builder.Property(v => v.CutDate);
        builder.Property(v => v.ReleasedDate);
        builder.Property(v => v.Notes).HasMaxLength(4000);

        builder.Property(v => v.StatusId).IsRequired();
        builder.Property(v => v.StatusWorkflowId).IsRequired();
        // Indexed for the reassignment migrator, whose only query is "every record on
        // this workflow". Unindexed it scans the whole table on every batch.
        builder.HasIndex(v => v.StatusWorkflowId);
        builder.Property(v => v.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(v => v.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(e => e.StatusAliasValue).IsRequired();
        builder.Property(v => v.StatusTransitionCount).IsRequired();

        builder.ConfigureStatusHistory();

        // Relationships
        // A version has no foreign key to the package or release it shipped in. Membership is recorded
        // by the manifest and by ReleaseVersions, and a second column saying the same thing could
        // disagree with them.
        builder.HasOne(v => v.Product)
            .WithMany()
            .HasForeignKey(v => v.ProductId)
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

        // Leads on ReleasedDate rather than ProductId, unlike the version index: ProductId is nullable
        // here and commonly null, so a leading-column filter on it would skip the announcements that
        // span product lines — the ones a "what shipped when" list most wants.
        builder.HasIndex(r => r.ReleasedDate)
            .IncludeProperties(r => new { r.Id, r.Key, r.Version, r.Name, r.Sequence, r.StatusCategory });

        builder.HasIndex(r => r.ProductId);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();

        // Nullable: a release spanning product lines has no single owner to name.
        builder.Property(r => r.ProductId);

        // Free text, never parsed. Nothing sorts or compares on this column.
        builder.Property(r => r.Version).IsRequired().HasMaxLength(128);

        builder.Property(r => r.Name).HasMaxLength(256);
        builder.Property(r => r.Sequence);
        builder.Property(r => r.TargetDate);
        builder.Property(r => r.ReleasedDate);
        builder.Property(r => r.Notes).HasMaxLength(4000);

        builder.Property(r => r.StatusId).IsRequired();
        builder.Property(r => r.StatusWorkflowId).IsRequired();
        builder.HasIndex(r => r.StatusWorkflowId);
        builder.Property(r => r.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(r => r.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(e => e.StatusAliasValue).IsRequired();
        builder.Property(r => r.StatusTransitionCount).IsRequired();

        builder.Ignore(r => r.IsEmpty);

        builder.ConfigureStatusHistory();

        // Relationships
        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Versions)
            .WithOne()
            .HasForeignKey(v => v.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Packages)
            .WithOne()
            .HasForeignKey(p => p.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(r => r.Packages).HasField("_packages").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ReleaseVersionConfiguration : IEntityTypeConfiguration<ReleaseVersion>
{
    public void Configure(EntityTypeBuilder<ReleaseVersion> builder)
    {
        builder.ToTable("ReleaseVersions", SchemaNames.Delivery);

        builder.HasKey(rv => rv.Id);

        builder.HasIndex(rv => new { rv.ReleaseId, rv.VersionId }).IsUnique();

        // A version may be announced by more than one release, so this is not unique on its own — the
        // rule a release enforces is that it must not carry the same version twice itself.
        builder.HasIndex(rv => rv.VersionId);

        builder.Property(rv => rv.Id).ValueGeneratedNever();
        builder.Property(rv => rv.ReleaseId).IsRequired();
        builder.Property(rv => rv.VersionId).IsRequired();

        // Relationships
        // Restrict, unlike the cascade from the release: removing a version that a release announces
        // would silently shrink what that release claims to have shipped.
        builder.HasOne(rv => rv.Version)
            .WithMany()
            .HasForeignKey(rv => rv.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReleasePackageInclusionConfiguration : IEntityTypeConfiguration<ReleasePackageInclusion>
{
    public void Configure(EntityTypeBuilder<ReleasePackageInclusion> builder)
    {
        builder.ToTable("ReleasePackageInclusions", SchemaNames.Delivery);

        builder.HasKey(rp => rp.Id);

        builder.HasIndex(rp => new { rp.ReleaseId, rp.PackageId }).IsUnique();

        // A package may serve more than one release — the same weekly shipment can carry work
        // announced under two product lines.
        builder.HasIndex(rp => rp.PackageId);

        builder.Property(rp => rp.Id).ValueGeneratedNever();
        builder.Property(rp => rp.ReleaseId).IsRequired();
        builder.Property(rp => rp.PackageId).IsRequired();

        // Relationships
        builder.HasOne(rp => rp.Package)
            .WithMany()
            .HasForeignKey(rp => rp.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(p => p.StatusWorkflowId).IsRequired();
        // Indexed for the reassignment migrator, whose only query is "every record on
        // this workflow". Unindexed it scans the whole table on every batch.
        builder.HasIndex(p => p.StatusWorkflowId);
        builder.Property(p => p.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(p => p.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(e => e.StatusAliasValue).IsRequired();
        builder.Property(p => p.StatusTransitionCount).IsRequired();

        builder.ConfigureStatusHistory();

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
        builder.Property(c => c.VersionId);
        builder.Property(c => c.Version).IsRequired().HasMaxLength(128);

        builder.Property(c => c.Kind).IsRequired()
            .HasConversion<EnumConverter<ManifestEntryKind>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // Relationships
        builder.HasOne(c => c.Product)
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

        // The delivery-metrics index: deployment frequency and change failure rate both filter
        // production deployments by outcome over a window.
        //
        // Keyed on "StatusAliasValue" by name, not on the Outcome property: Outcome is computed and
        // Ignore()d below, and EF silently drops a HasIndex over an ignored property — the index simply
        // never reached the schema. Named this way it indexes the real column the alias lives in.
        builder.HasIndex(
                nameof(Deployment.EnvironmentCategory),
                nameof(Deployment.StatusAliasValue),
                nameof(Deployment.CompletedAt))
            .IncludeProperties(
                nameof(Deployment.Id),
                nameof(Deployment.Key),
                nameof(Deployment.VersionId),
                nameof(Deployment.PackageId),
                nameof(Deployment.EnvironmentId));

        builder.HasIndex(d => d.VersionId);
        builder.HasIndex(d => d.PackageId);
        builder.HasIndex(d => new { d.EnvironmentId, d.StartedAt });

        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Key).ValueGeneratedOnAdd();

        builder.Property(d => d.VersionId);
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
        builder.Property(d => d.StatusWorkflowId).IsRequired();
        // Indexed for the reassignment migrator, whose only query is "every record on
        // this workflow". Unindexed it scans the whole table on every batch.
        builder.HasIndex(d => d.StatusWorkflowId);
        builder.Property(d => d.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(d => d.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(e => e.StatusAliasValue).IsRequired();
        builder.Property(d => d.StatusTransitionCount).IsRequired();

        builder.ConfigureStatusHistory();

        // Relationships
        builder.HasOne(d => d.Version)
            .WithMany()
            .HasForeignKey(d => d.VersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Package)
            .WithMany()
            .HasForeignKey(d => d.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Environment)
            .WithMany()
            .HasForeignKey(d => d.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore
        builder.Ignore(d => d.Outcome);
        builder.Ignore(d => d.IsComplete);
        builder.Ignore(d => d.IsChangeFailure);
    }
}
