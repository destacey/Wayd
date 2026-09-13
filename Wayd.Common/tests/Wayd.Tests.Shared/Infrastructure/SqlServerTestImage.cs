namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>The SQL Server image every Testcontainers suite runs against.</summary>
/// <remarks>
/// Pinned to a concrete CU rather than a floating tag, so the schema is built against the same engine on
/// every machine and CI run. Bump it deliberately, here, and every suite moves together.
/// .github/scripts/dotnet-test-projects.sh reads the tag from this file to pull the image before the suites
/// start, so keep it a single string literal.
/// </remarks>
public static class SqlServerTestImage
{
    public const string Name = "mcr.microsoft.com/mssql/server:2025-CU8-ubuntu-24.04";
}
