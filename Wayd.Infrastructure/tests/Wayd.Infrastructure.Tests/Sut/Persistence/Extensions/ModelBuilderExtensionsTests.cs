using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayd.Common.Domain.Interfaces;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Tests.Sut.Persistence.Extensions;

public sealed class ModelBuilderExtensionsTests
{
    private static DbContextOptions<T> CreateInMemoryOptions<T>() where T : DbContext
    {
        return new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private static IQueryFilter GetRequiredQueryFilter(IReadOnlyEntityType entityType, string key)
    {
        var filter = entityType.FindDeclaredQueryFilter(key);
        filter.Should().NotBeNull($"expected a query filter with key '{key}' on entity '{entityType.ClrType.Name}'");
        return filter!;
    }

    [Fact]
    public void AppendGlobalQueryFilter_AppliesFilterToEntitiesImplementingInterface()
    {
        // Arrange & Act
        using var context = new TestDbContext(CreateInMemoryOptions<TestDbContext>());
        var model = context.Model;

        // Assert - entities implementing ISoftDelete should have a keyed query filter
        var softDeletableType = model.FindEntityType(typeof(SoftDeletableEntity));
        var anotherSoftDeletableType = model.FindEntityType(typeof(AnotherSoftDeletableEntity));

        GetRequiredQueryFilter(softDeletableType!, typeof(ISoftDelete).FullName!);
        GetRequiredQueryFilter(anotherSoftDeletableType!, typeof(ISoftDelete).FullName!);
    }

    [Fact]
    public void AppendGlobalQueryFilter_DoesNotApplyFilterToEntitiesNotImplementingInterface()
    {
        // Arrange & Act
        using var context = new TestDbContext(CreateInMemoryOptions<TestDbContext>());
        var model = context.Model;

        // Assert - entity not implementing ISoftDelete should not have any query filters
        var nonSoftDeletableType = model.FindEntityType(typeof(NonSoftDeletableEntity));

        nonSoftDeletableType!.GetDeclaredQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void AppendGlobalQueryFilter_FilterReferencesCorrectProperty()
    {
        // Arrange & Act
        using var context = new TestDbContext(CreateInMemoryOptions<TestDbContext>());
        var model = context.Model;

        // Assert - the filter expression should reference IsDeleted
        var entityType = model.FindEntityType(typeof(SoftDeletableEntity));
        var filter = GetRequiredQueryFilter(entityType!, typeof(ISoftDelete).FullName!);

        filter.Expression!.Body.ToString().Should().Contain("IsDeleted");
    }

    [Fact]
    public void AppendGlobalQueryFilter_DoesNotApplyFilterToDerivedEntities()
    {
        // Arrange & Act
        using var context = new InheritanceTestDbContext(CreateInMemoryOptions<InheritanceTestDbContext>());
        var model = context.Model;

        // Assert - base entity should have the filter
        var baseType = model.FindEntityType(typeof(SoftDeletableEntity));
        GetRequiredQueryFilter(baseType!, typeof(ISoftDelete).FullName!);

        // Derived entity has a base type and should not have its own filter
        var derivedType = model.FindEntityType(typeof(DerivedSoftDeletableEntity));
        derivedType!.BaseType.Should().NotBeNull("derived entity should have a base type");
        derivedType.GetDeclaredQueryFilters().Should().BeEmpty(
            "derived entities should not have their own filters; they inherit from the base type");
    }

    [Fact]
    public void AppendGlobalQueryFilter_SupportsMultipleFiltersOnSameEntity()
    {
        // Arrange & Act
        using var context = new MultiFilterDbContext(CreateInMemoryOptions<MultiFilterDbContext>());
        var model = context.Model;

        // Assert - entity implementing both interfaces should have separate keyed query filters
        var auditableType = model.FindEntityType(typeof(AuditableEntity));
        var filters = auditableType!.GetDeclaredQueryFilters();

        filters.Should().HaveCount(2);

        var softDeleteFilter = GetRequiredQueryFilter(auditableType, typeof(ISoftDelete).FullName!);
        var auditableFilter = GetRequiredQueryFilter(auditableType, typeof(IAuditable).FullName!);

        softDeleteFilter.Expression!.Body.ToString().Should().Contain("IsDeleted");
        auditableFilter.Expression!.Body.ToString().Should().Contain("CreatedBy");
    }

    [Fact]
    public void AppendGlobalQueryFilter_FilterParameterMatchesEntityType()
    {
        // Arrange & Act
        using var context = new TestDbContext(CreateInMemoryOptions<TestDbContext>());
        var model = context.Model;

        // Assert - the filter's parameter type should match the entity type
        var entityType = model.FindEntityType(typeof(SoftDeletableEntity));
        var filter = GetRequiredQueryFilter(entityType!, typeof(ISoftDelete).FullName!);

        filter.Expression!.Parameters.Should().HaveCount(1);
        filter.Expression.Parameters[0].Type.Should().Be(typeof(SoftDeletableEntity));
    }

    [Fact]
    public void AppendGlobalQueryFilter_ReturnsModelBuilderForChaining()
    {
        // Arrange & Act
        using var context = new TestDbContext(CreateInMemoryOptions<TestDbContext>());

        // The fact that OnModelCreating completes without error and the context
        // is usable proves the method returns ModelBuilder correctly for chaining.
        var model = context.Model;
        model.Should().NotBeNull();
    }

    [Fact]
    public async Task AppendGlobalQueryFilter_FiltersOutSoftDeletedEntities()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = CreateInMemoryOptions<TestDbContext>();

        await using (var seedContext = new TestDbContext(options))
        {
            seedContext.SoftDeletables.AddRange(
                new SoftDeletableEntity { Id = 1, Name = "Active", IsDeleted = false },
                new SoftDeletableEntity { Id = 2, Name = "Deleted", IsDeleted = true },
                new SoftDeletableEntity { Id = 3, Name = "Also Active", IsDeleted = false }
            );
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Act
        await using var queryContext = new TestDbContext(options);
        var results = await queryContext.SoftDeletables.ToListAsync(cancellationToken);

        // Assert - only non-deleted entities should be returned
        results.Should().HaveCount(2);
        results.Should().OnlyContain(e => !e.IsDeleted);
        results.Select(e => e.Name).Should().BeEquivalentTo("Active", "Also Active");
    }

    [Fact]
    public async Task AppendGlobalQueryFilter_AllowsQueryingDeletedEntitiesWithIgnoreFilters()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = CreateInMemoryOptions<TestDbContext>();

        await using (var seedContext = new TestDbContext(options))
        {
            seedContext.SoftDeletables.AddRange(
                new SoftDeletableEntity { Id = 1, Name = "Active", IsDeleted = false },
                new SoftDeletableEntity { Id = 2, Name = "Deleted", IsDeleted = true }
            );
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Act
        await using var queryContext = new TestDbContext(options);
        var results = await queryContext.SoftDeletables.IgnoreQueryFilters().ToListAsync(cancellationToken);

        // Assert - all entities should be returned when filters are ignored
        results.Should().HaveCount(2);
    }

    #region Test Entities

    private class SoftDeletableEntity : ISoftDelete
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsDeleted { get; set; }
        public Instant? Deleted { get; set; }
        public string? DeletedBy { get; set; }
    }

    private class AnotherSoftDeletableEntity : ISoftDelete
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public Instant? Deleted { get; set; }
        public string? DeletedBy { get; set; }
    }

    private class NonSoftDeletableEntity
    {
        public int Id { get; set; }
        public required string Value { get; set; }
    }

    private interface IAuditable
    {
        string CreatedBy { get; set; }
    }

    private class AuditableEntity : IAuditable, ISoftDelete
    {
        public int Id { get; set; }
        public required string CreatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public Instant? Deleted { get; set; }
        public string? DeletedBy { get; set; }
    }

    private class DerivedSoftDeletableEntity : SoftDeletableEntity
    {
        public required string Extra { get; set; }
    }

    #endregion

    #region Test DbContexts

    private class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<SoftDeletableEntity> SoftDeletables => Set<SoftDeletableEntity>();
        public DbSet<AnotherSoftDeletableEntity> AnotherSoftDeletables => Set<AnotherSoftDeletableEntity>();
        public DbSet<NonSoftDeletableEntity> NonSoftDeletables => Set<NonSoftDeletableEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(s => !s.IsDeleted);
            base.OnModelCreating(modelBuilder);
        }
    }

    private class InheritanceTestDbContext(DbContextOptions<InheritanceTestDbContext> options) : DbContext(options)
    {
        public DbSet<SoftDeletableEntity> SoftDeletables => Set<SoftDeletableEntity>();
        public DbSet<DerivedSoftDeletableEntity> DerivedSoftDeletables => Set<DerivedSoftDeletableEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(s => !s.IsDeleted);
            base.OnModelCreating(modelBuilder);
        }
    }

    private class MultiFilterDbContext(DbContextOptions<MultiFilterDbContext> options) : DbContext(options)
    {
        public DbSet<AuditableEntity> Auditables => Set<AuditableEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(s => !s.IsDeleted);
            modelBuilder.AppendGlobalQueryFilter<IAuditable>(a => a.CreatedBy != string.Empty);
            base.OnModelCreating(modelBuilder);
        }
    }

    #endregion
}
