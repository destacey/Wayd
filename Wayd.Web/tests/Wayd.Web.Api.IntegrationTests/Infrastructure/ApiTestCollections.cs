namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Shares a single <see cref="WaydSqlServerApiFactory"/> — and therefore one SQL Server container, one
/// migrated schema and one booted host — across every test class that needs a real database. As an
/// <c>IClassFixture</c> this factory was constructed once per test class, so six classes paid six container
/// starts and six host boots (~16s each) end to end.
/// <para>
/// The factory's process-global environment variables (see <see cref="WaydSqlServerApiFactory"/>) make a
/// single shared instance the safer arrangement as well as the faster one: only one factory now sets them.
/// Tests share a database, so each must use its own data rather than assuming an empty schema.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerApiTestCollection : ICollectionFixture<WaydSqlServerApiFactory>
{
    public const string Name = "SqlServer API integration tests";
}

/// <summary>
/// Shares a single <see cref="WaydApiFactory"/> across the test classes that only need the host to boot
/// against the EF in-memory provider. No container, but a host boot each, so the same per-class cost applied.
/// </summary>
[CollectionDefinition(Name)]
public sealed class InMemoryApiTestCollection : ICollectionFixture<WaydApiFactory>
{
    public const string Name = "In-memory API integration tests";
}
