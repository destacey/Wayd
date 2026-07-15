namespace Wayd.Organization.IntegrationTests.Infrastructure;

/// <summary>
/// Shares a single <see cref="SqlServerDbContextFixture"/> — and therefore one SQL Server container and one
/// migrated schema — across every integration test class in this project. Each test resets the Organization
/// data it touches, so the classes are isolated without paying the per-class container start-up cost.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerDbContextFixture>
{
    public const string Name = "SqlServer integration tests";
}
