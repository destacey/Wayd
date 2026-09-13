using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Shared mapping for a module's copy of another module's record, the rows kept by replication handlers.
/// </summary>
public static class ReplicaEntityBuilderExtensions
{
    private static readonly JsonSerializerOptions WatermarkSerializerOptions =
        new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    /// <summary>
    /// Maps the copy's watermarks as one JSON column and gives the row a concurrency token.
    /// </summary>
    /// <remarks>
    /// The watermarks are only ever read with their row and compared in memory, so a column per group would
    /// widen the table for nothing. They go through a value converter rather than <c>ToJson</c> because EF
    /// only accepts <c>nvarchar(max)</c> for a JSON-mapped column; a group adds about 50 characters, and
    /// overflowing the bound fails the save instead of truncating.
    /// <para>
    /// The column is written whole, so two handlers applying changes to different groups of the same copy
    /// would each put back the other's old watermark, and a later stale event could then slip through. The
    /// row version turns that race into a concurrency conflict, which the durable failure policy retries
    /// against fresh state.
    /// </para>
    /// </remarks>
    public static EntityTypeBuilder<TEntity> ConfigureReplicaTracking<TEntity, TWatermarks>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, TWatermarks>> watermarks)
        where TEntity : class
    {
        builder.Property(watermarks)
            .HasJsonConversion(WatermarkSerializerOptions)
            .HasColumnName("Watermarks")
            .HasColumnType("varchar(1024)")
            .IsRequired();

        builder.Property<byte[]>("Version").IsRowVersion();

        return builder;
    }
}
