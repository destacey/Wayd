using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeliveryMetrics.Dtos;

namespace Wayd.ProductManagement.Application.DeliveryMetrics.Queries;

/// <summary>
/// The delivery measures over a window.
/// </summary>
/// <param name="From">Start of the window, inclusive.</param>
/// <param name="To">End of the window, inclusive.</param>
/// <param name="ProductId">
/// Narrows to deployments of one product's releases. Package deployments are excluded when this is set,
/// because a package spans several products and attributing it to one would overcount.
/// </param>
public sealed record GetDeliveryMetricsQuery(Instant From, Instant To, Guid? ProductId = null)
    : IQuery<DeliveryMetricsDto>;

public sealed class GetDeliveryMetricsQueryValidator : AbstractValidator<GetDeliveryMetricsQuery>
{
    public GetDeliveryMetricsQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The end of the window cannot be before its start.");
    }
}

public sealed class GetDeliveryMetricsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeliveryMetricsQuery, DeliveryMetricsDto>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    /// <summary>
    /// The measures this module cannot compute, and what each is waiting on.
    /// </summary>
    /// <remarks>
    /// Stated rather than omitted. Both need data the module does not record, and approximating either
    /// from what it does record would produce a number that looks right and means something else.
    /// </remarks>
    private static readonly UnavailableMetricDto[] NotYetMeasured =
    [
        new()
        {
            Metric = "Lead time for changes",
            Reason =
                "Needs a link from a deployment back to the change it carried — a commit or work item. "
                + "A deployment records what shipped, not what went into it. The release cut-to-ship "
                + "interval is a different measure and is not a substitute.",
        },
        new()
        {
            Metric = "Time to restore service",
            Reason =
                "Needs an incident record: when service degraded and when it recovered. A rollback "
                + "timestamp is when the change was reverted, which is not the same as when service "
                + "was restored.",
        },
    ];

    public async Task<DeliveryMetricsDto> Handle(GetDeliveryMetricsQuery query, CancellationToken cancellationToken)
    {
        // Completed production deployments only. In-flight ones have not been delivered, and the
        // frozen EnvironmentCategory is what makes a later reclassification unable to move a past
        // deployment in or out of this set.
        var deployments = _productManagementDbContext.Deployments
            .AsNoTracking()
            .Where(d => d.EnvironmentCategory == EnvironmentCategory.Production)
            .Where(d => d.CompletedAt != null && d.CompletedAt >= query.From && d.CompletedAt <= query.To);

        if (query.ProductId is not null)
        {
            // Version deployments only: a package spans several products, so attributing one to a
            // single product would count the same shipment under each of them.
            deployments = deployments.Where(d => d.VersionId != null
                && _productManagementDbContext.Versions
                    .Any(v => v.Id == d.VersionId && v.ProductId == query.ProductId));
        }

        // One trip: the alias breakdown answers both measures. StatusAliasValue is a mapped column,
        // so this translates in SQL and still runs under LINQ-to-Objects for the handler tests.
        // StatusCategory could not serve here — it puts Failed and RolledBack both in Removed, and
        // frequency has to tell them apart.
        var byOutcome = await deployments
            .GroupBy(d => d.StatusAliasValue)
            .Select(g => new { Alias = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var total = byOutcome.Sum(o => o.Count);

        // Reached production: succeeded and rolled back alike. A rollback did deploy — whether that was
        // a good idea is change failure rate's question, not this one.
        //
        // Enumerated positively rather than as "not Failed": aliases are user-extensible, so an
        // org-invented deployment status carries alias 0 and would otherwise count as a delivery.
        var delivered = byOutcome
            .Where(o => o.Alias == (int)ProductStatusAlias.Succeeded || o.Alias == (int)ProductStatusAlias.RolledBack)
            .Sum(o => o.Count);

        var failed = byOutcome
            .Where(o => o.Alias == (int)ProductStatusAlias.Failed || o.Alias == (int)ProductStatusAlias.RolledBack)
            .Sum(o => o.Count);

        var windowDays = (query.To - query.From).TotalDays;

        return new DeliveryMetricsDto
        {
            From = query.From,
            To = query.To,
            DeploymentFrequency = new DeploymentFrequencyDto
            {
                Count = delivered,
                WindowDays = windowDays,
            },
            ChangeFailureRate = new ChangeFailureRateDto
            {
                TotalDeployments = total,
                FailedDeployments = failed,
            },
            Unavailable = NotYetMeasured,
        };
    }
}
