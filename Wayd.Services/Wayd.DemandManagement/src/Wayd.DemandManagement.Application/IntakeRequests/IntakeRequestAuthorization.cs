using Wayd.Common.Domain.Authorization;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Resolves who is reading or changing intake requests: their permissions and the desks they review on.
/// </summary>
/// <remarks>
/// Controllers check the permission an endpoint needs; it is checked again here because Review is decided
/// per desk, and because the DTOs answer the same questions for the UI, which must agree with what the
/// commands accept.
/// </remarks>
public static class IntakeRequestAuthorization
{
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.IntakeRequests);

    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.IntakeRequests);

    public static readonly string ReviewPermission =
        ApplicationPermission.NameFor(ApplicationAction.Review, ApplicationResource.IntakeRequests);

    public static readonly string AdministerPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.IntakeRequests);

    public const string NotAReviewerError = "Only reviewers on the request's desk can do this.";

    /// <summary>
    /// The current user as a reader of intake requests.
    /// </summary>
    public static async Task<IntakeRequestReader> ResolveIntakeRequestReader(
        this ICurrentPrincipal currentPrincipal,
        IDemandManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        var holdsReview = await currentPrincipal.HasPermission(ReviewPermission, cancellationToken);
        var holdsAdminister = await currentPrincipal.HasPermission(AdministerPermission, cancellationToken);

        Guid[] reviewerDeskIds = holdsReview && !holdsAdminister && employeeId is { } id
            ? await dbContext.IntakeDesks
                .Where(d => d.Reviewers.Any(r => r.EmployeeId == id))
                .Select(d => d.Id)
                .ToArrayAsync(cancellationToken)
            : [];

        return new IntakeRequestReader(employeeId, holdsReview, holdsAdminister, reviewerDeskIds);
    }
}
