using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application;

public interface IProductManagementDbContext : IWaydDbContext
{
    DbSet<Product> Products { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<ProductTagCategory> ProductTagCategories { get; }
    DbSet<ProductTag> ProductTags { get; }
    DbSet<ProductTagAssignment> ProductTagAssignments { get; }
    DbSet<Release> Releases { get; }
    DbSet<ReleasePackage> ReleasePackages { get; }
    DbSet<ReleasePackageComponent> ReleasePackageComponents { get; }
    DbSet<DeploymentEnvironment> DeploymentEnvironments { get; }
    DbSet<Deployment> Deployments { get; }
}
