using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Infrastructure.Persistence.Configuration;

/// <summary>
/// Shared mapping for the status history every <see cref="StatusTrackedEntity"/> carries.
/// </summary>
public static class StatusTrackedEntityBuilderExtensions
{
    /// <summary>
    /// Maps the transition history for a status-tracked aggregate.
    /// </summary>
    /// <remarks>
    /// The collection is <strong>not</strong> a navigation. One <c>StatusTransitions</c> table serves every
    /// owner type, so a per-aggregate relationship puts one foreign key per aggregate on the same
    /// <see cref="StatusTransition.RecordId"/> column — and a row would then have to be a Product
    /// <em>and</em> a Release <em>and</em> a Deployment at once, which no insert can satisfy. The
    /// <see cref="StatusTransition.OwnerType"/> discriminator is what makes the rows unambiguous.
    /// <para>
    /// Ignoring it is therefore right, but ignoring it <em>alone</em> is a silent data-loss bug:
    /// <c>ApplyStatus</c> appends to the aggregate's list and EF never sees the new row.
    /// <c>BaseDbContext.CollectStatusTransitions</c> is the other half — it drains those appends into the
    /// set on save. Domain tests assert against the same in-memory list and pass either way, so only a
    /// database round-trip tells the difference.
    /// </para>
    /// </remarks>
    public static EntityTypeBuilder<TEntity> ConfigureStatusHistory<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : StatusTrackedEntity
    {
        builder.Ignore(e => e.StatusTransitions);

        return builder;
    }
}
