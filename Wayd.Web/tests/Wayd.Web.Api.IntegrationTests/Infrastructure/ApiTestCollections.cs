namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Shares a single <see cref="WaydSqlServerApiFactory"/> — one database, migrated schema and booted host — across every
/// test class that needs a real database. Tests share that database, so each uses its own data rather than assuming an
/// empty schema.
/// </summary>
/// <remarks>
/// One collection rather than several in parallel: the suite is CPU-bound, and on a 4-core CI runner parallel collections
/// are slower, since each adds a host, a migration and a warm-up the runner has no cores spare for. Hosts share no
/// process-wide configuration, so a split worth trying on a larger runner is a change to this file alone.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class SqlServerApiTestCollection : ICollectionFixture<WaydSqlServerApiFactory>
{
    public const string Name = "SqlServer API integration tests";
}

/// <summary>
/// Shares a single <see cref="WaydApiFactory"/> across the test classes that only need the host to boot
/// against the EF in-memory provider. No database, but a host boot, so one per collection rather than per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class InMemoryApiTestCollection : ICollectionFixture<WaydApiFactory>
{
    public const string Name = "In-memory API integration tests";
}
