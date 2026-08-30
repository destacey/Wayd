using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Domain.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Infrastructure;

/// <summary>
/// An in-memory <see cref="IProductManagementDbContext"/> so handler tests need no Moq setup per DbSet.
/// </summary>
public class FakeProductManagementDbContext : IProductManagementDbContext, IStatusWorkflowDbContext, IDisposable
{
    private readonly List<Product> _products = [];
    private readonly List<ProductType> _productTypes = [];
    private readonly List<ProductTagCategory> _productTagCategories = [];
    private readonly List<ProductTag> _productTags = [];
    private readonly List<ProductTagAssignment> _productTagAssignments = [];
    private readonly List<Release> _releases = [];
    private readonly List<ReleasePackage> _releasePackages = [];
    private readonly List<ReleasePackageComponent> _releasePackageComponents = [];
    private readonly List<DeploymentEnvironment> _deploymentEnvironments = [];
    private readonly List<Deployment> _deployments = [];

    private readonly List<StatusWorkflow> _statusWorkflows = [];
    private readonly List<WorkflowAssignment> _workflowAssignments = [];

    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<ExternalIdentityMapping> _externalIdentityMappings = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];
    private readonly List<ScoringModel> _scoringModels = [];

    public DbSet<Product> Products => _products.AsDbSet();
    public DbSet<ProductType> ProductTypes => _productTypes.AsDbSet();
    public DbSet<ProductTagCategory> ProductTagCategories => _productTagCategories.AsDbSet();
    public DbSet<ProductTag> ProductTags => _productTags.AsDbSet();
    public DbSet<ProductTagAssignment> ProductTagAssignments => _productTagAssignments.AsDbSet();
    public DbSet<Release> Releases => _releases.AsDbSet();
    public DbSet<ReleasePackage> ReleasePackages => _releasePackages.AsDbSet();
    public DbSet<ReleasePackageComponent> ReleasePackageComponents => _releasePackageComponents.AsDbSet();
    public DbSet<DeploymentEnvironment> DeploymentEnvironments => _deploymentEnvironments.AsDbSet();
    public DbSet<Deployment> Deployments => _deployments.AsDbSet();

    public DbSet<StatusWorkflow> StatusWorkflows => _statusWorkflows.AsDbSet();
    public DbSet<WorkflowAssignment> WorkflowAssignments => _workflowAssignments.AsDbSet();

    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<ExternalIdentityMapping> ExternalIdentityMappings => _externalIdentityMappings.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();

    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException(
        "Database operations are not supported in FakeProductManagementDbContext. Use integration tests with a real DbContext.");

    /// <summary>
    /// How many times the handler saved. Asserted on to catch a handler that mutates without persisting.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        return Task.FromResult(_products.Count + _productTypes.Count + _releases.Count);
    }

    // Reload is how handlers discard a failed mutation; nothing here tracks state to reload, so the
    // failure paths need integration coverage rather than a fake that pretends.
    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class =>
        throw new NotImplementedException("Entry tracking is not supported in FakeProductManagementDbContext.");

    public EntityEntry Entry(object entity) =>
        throw new NotImplementedException("Entry tracking is not supported in FakeProductManagementDbContext.");

    #region Helper Methods for Test Setup

    public void AddProduct(Product product) => _products.Add(product);
    public void AddProducts(IEnumerable<Product> products) => _products.AddRange(products);

    public void AddProductType(ProductType productType) => _productTypes.Add(productType);
    public void AddProductTypes(IEnumerable<ProductType> productTypes) => _productTypes.AddRange(productTypes);

    public void AddProductTagCategory(ProductTagCategory category) => _productTagCategories.Add(category);
    public void AddProductTag(ProductTag tag) => _productTags.Add(tag);
    public void AddProductTagAssignment(ProductTagAssignment assignment) => _productTagAssignments.Add(assignment);
    public void AddProductTags(IEnumerable<ProductTag> tags) => _productTags.AddRange(tags);

    public void AddRelease(Release release) => _releases.Add(release);
    public void AddReleases(IEnumerable<Release> releases) => _releases.AddRange(releases);

    public void AddStatusWorkflow(StatusWorkflow workflow) => _statusWorkflows.Add(workflow);
    public void AddWorkflowAssignment(WorkflowAssignment assignment) => _workflowAssignments.Add(assignment);

    public void Clear()
    {
        _products.Clear();
        _productTypes.Clear();
        _productTagCategories.Clear();
        _productTags.Clear();
        _productTagAssignments.Clear();
        _releases.Clear();
        _releasePackages.Clear();
        _releasePackageComponents.Clear();
        _deploymentEnvironments.Clear();
        _deployments.Clear();
        _statusWorkflows.Clear();
        _workflowAssignments.Clear();
        _employees.Clear();
        SaveChangesCallCount = 0;
    }

    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    #endregion
}
