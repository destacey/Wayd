using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Shared mapping for a record that carries <see cref="OwnershipInfo"/>.
/// </summary>
public static class OwnershipEntityBuilderExtensions
{
    /// <summary>The column holding who writes the record.</summary>
    public const string OwnershipColumn = "Ownership";

    /// <summary>The column holding the kind of outside system the record corresponds to.</summary>
    public const string ConnectorColumn = "Connector";

    /// <summary>The column holding the outside system's identifier for its instance.</summary>
    public const string SystemIdColumn = "SystemId";

    /// <summary>The column holding the record's identifier in the outside system.</summary>
    public const string ExternalIdColumn = "ExternalId";

    /// <summary>
    /// Maps the record's ownership to four columns on its own table, the same on every record that has it.
    /// </summary>
    public static EntityTypeBuilder<TEntity> ConfigureOwnershipInfo<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IHasOwnershipInfo
    {
        // By name, not by lambda: an expression over TEntity resolves to the interface's member, not the entity's.
        builder.ComplexProperty<OwnershipInfo>(nameof(IHasOwnershipInfo.OwnershipInfo), options =>
        {
            options.Property(o => o.Ownership).HasColumnName(OwnershipColumn)
                .HasConversion<EnumConverter<Ownership>>()
                .HasColumnType("varchar")
                .HasMaxLength(32)
                .IsRequired();
            options.Property(o => o.Connector).HasColumnName(ConnectorColumn)
                .HasConversion<EnumConverter<Connector>>()
                .HasColumnType("varchar")
                .HasMaxLength(32);
            options.Property(o => o.SystemId).HasColumnName(SystemIdColumn)
                .HasColumnType("varchar")
                .HasMaxLength(64);
            options.Property(o => o.ExternalId).HasColumnName(ExternalIdColumn)
                .HasColumnType("varchar")
                .HasMaxLength(64);
        });

        return builder;
    }
}
