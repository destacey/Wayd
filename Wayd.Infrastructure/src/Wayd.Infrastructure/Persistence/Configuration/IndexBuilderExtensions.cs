using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// The filtered-index predicates the model uses, written once. A filter is a string in the provider's SQL
/// dialect, so this is the only file that knows how a column is quoted or how a boolean is spelled.
/// </summary>
public static class IndexBuilderExtensions
{
    /// <summary>Index only rows that are not soft-deleted.</summary>
    public static IndexBuilder<TEntity> WhereNotDeleted<TEntity>(this IndexBuilder<TEntity> builder) =>
        builder.HasFilter(NotDeleted);

    /// <summary>Index only rows that are not soft-deleted.</summary>
    public static IndexBuilder WhereNotDeleted(this IndexBuilder builder) =>
        builder.HasFilter(NotDeleted);

    /// <summary>Index only rows flagged active.</summary>
    public static IndexBuilder<TEntity> WhereActive<TEntity>(this IndexBuilder<TEntity> builder) =>
        builder.HasFilter(Active);

    /// <summary>Index only rows flagged active.</summary>
    public static IndexBuilder WhereActive(this IndexBuilder builder) =>
        builder.HasFilter(Active);

    /// <summary>Index only rows where <paramref name="column"/> is null.</summary>
    public static IndexBuilder<TEntity> WhereNull<TEntity>(this IndexBuilder<TEntity> builder, string column) =>
        builder.HasFilter(IsNull(column));

    /// <summary>Index only rows where <paramref name="column"/> is null.</summary>
    public static IndexBuilder WhereNull(this IndexBuilder builder, string column) =>
        builder.HasFilter(IsNull(column));

    /// <summary>Index only rows where <paramref name="column"/> is not null.</summary>
    public static IndexBuilder<TEntity> WhereNotNull<TEntity>(this IndexBuilder<TEntity> builder, string column) =>
        builder.HasFilter(IsNotNull(column));

    /// <summary>Index only rows where <paramref name="column"/> is not null.</summary>
    public static IndexBuilder WhereNotNull(this IndexBuilder builder, string column) =>
        builder.HasFilter(IsNotNull(column));

    private const string NotDeleted = "[IsDeleted] = 0";
    private const string Active = "[IsActive] = 1";
    private static string IsNull(string column) => $"[{column}] IS NULL";
    private static string IsNotNull(string column) => $"[{column}] IS NOT NULL";
}
