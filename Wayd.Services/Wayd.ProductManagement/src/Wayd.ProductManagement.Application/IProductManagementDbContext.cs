using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version. Aliased wherever both are in scope, since
// ImplicitUsings puts System in every file.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application;

public interface IProductManagementDbContext : IWaydDbContext
{
    DbSet<Product> Products { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<ProductTagCategory> ProductTagCategories { get; }
    DbSet<ProductTag> ProductTags { get; }
    DbSet<ProductTagAssignment> ProductTagAssignments { get; }
    DbSet<Version> Versions { get; }
    DbSet<Release> Releases { get; }
    DbSet<ReleaseVersion> ReleaseVersions { get; }
    DbSet<ReleasePackageInclusion> ReleasePackageInclusions { get; }
    DbSet<ReleasePackage> ReleasePackages { get; }
    DbSet<ReleasePackageComponent> ReleasePackageComponents { get; }
    DbSet<DeploymentEnvironment> DeploymentEnvironments { get; }
    DbSet<Deployment> Deployments { get; }
}
