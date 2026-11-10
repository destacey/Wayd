using CSharpFunctionalExtensions;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Validates incident references to severity levels and products.
/// </summary>
internal static class IncidentReferenceValidation
{
    internal static async Task<Result> ValidateSeverityLevelInSelectedSet(
        IProductManagementDbContext context,
        Guid severityLevelId,
        CancellationToken cancellationToken)
    {
        var selectedSetId = await context.IncidentSeveritySetAssignments
            .Select(a => a.SeveritySetId)
            .SingleAsync(cancellationToken);

        var inSet = await context.IncidentSeverityLevels
            .AnyAsync(l => l.Id == severityLevelId && l.SetId == selectedSetId, cancellationToken);

        return inSet
            ? Result.Success()
            : Result.Failure("The severity level is not in the organization's selected severity set.");
    }

    internal static async Task<Result> ValidateProductsExist(
        IProductManagementDbContext context,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var distinct = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return Result.Success();
        }

        var found = await context.Products.CountAsync(p => distinct.Contains(p.Id), cancellationToken);
        return found == distinct.Count
            ? Result.Success()
            : Result.Failure("One or more products were not found.");
    }

    internal static async Task<Result> ValidateReleasesExist(
        IProductManagementDbContext context,
        IReadOnlyCollection<Guid> releaseIds,
        CancellationToken cancellationToken)
    {
        var distinct = releaseIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return Result.Success();
        }

        var found = await context.Releases.CountAsync(r => distinct.Contains(r.Id), cancellationToken);
        return found == distinct.Count
            ? Result.Success()
            : Result.Failure("One or more releases were not found.");
    }
}
