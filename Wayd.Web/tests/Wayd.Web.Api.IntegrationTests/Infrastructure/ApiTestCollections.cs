namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

// The SQL Server collections run in parallel, each with a WaydSqlServerApiFactory of its own: its own database,
// migrated schema and booted host. Tests within a collection run one at a time and share its database, so each uses
// its own data rather than assuming an empty schema. A collection costs a host boot and a migration, so the suite has
// a few large ones, split by how long their tests take: a class slow enough to hold the others back gets a collection
// to itself or with others like it, and every other class goes in SqlServerApiTestCollection.

/// <summary>Every SQL Server-backed test class without a collection of its own.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerApiTestCollection : ICollectionFixture<WaydSqlServerApiFactory>
{
    public const string Name = "SqlServer API integration tests";
}

/// <summary>The per-user rate limit tests, each booting a host of its own with lowered limits.</summary>
[CollectionDefinition(Name)]
public sealed class PerUserRateLimitApiTestCollection : ICollectionFixture<WaydSqlServerApiFactory>
{
    public const string Name = "SqlServer API integration tests: per-user rate limits";
}

/// <summary>Sign-in, local tokens and personal access tokens, with the sign-in rate limit tests.</summary>
[CollectionDefinition(Name)]
public sealed class SignInApiTestCollection : ICollectionFixture<WaydSqlServerApiFactory>
{
    public const string Name = "SqlServer API integration tests: sign-in and tokens";
}

/// <summary>The OAuth authorization server and the hosted MCP endpoint it issues tokens for.</summary>
[CollectionDefinition(Name)]
public sealed class OAuthAndMcpApiTestCollection : ICollectionFixture<WaydSqlServerApiFactory>
{
    public const string Name = "SqlServer API integration tests: OAuth and MCP";
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
