using Microsoft.EntityFrameworkCore;

namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>
/// The failed saves a fake context can throw, shaped as SQL Server reports them: a <see cref="DbUpdateException"/>
/// over a provider exception carrying the error <c>Number</c>, as <c>SqlException</c> does.
/// </summary>
public static class SaveFailures
{
    /// <summary>A save that met another row at a unique index (SQL Server error 2601).</summary>
    public static DbUpdateException UniqueKey() => new("Cannot insert duplicate key row.", new ProviderError(2601));

    /// <summary>A save that failed for another reason: a value too long for its column (SQL Server error 2628).</summary>
    public static DbUpdateException Truncation() => new("String or binary data would be truncated.", new ProviderError(2628));

    /// <summary>Stands in for the provider's exception, which tests cannot construct.</summary>
    private sealed class ProviderError(int number) : Exception($"SQL error {number}.")
    {
        public int Number { get; } = number;
    }
}
