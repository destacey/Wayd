using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks;

/// <summary>
/// What starting a desk's poker session and adding requests to one share: who may, and the requests as Planning
/// is told about them.
/// </summary>
internal static class IntakeDeskPokerSessions
{
    /// <summary>The failure for a caller who does not work the desk whose session they are setting up.</summary>
    public const string NotAnAgentError = "Only agents on the desk can run its poker sessions.";

    /// <summary>The failure for a desk with no estimation scale, whose requests a session could not size.</summary>
    public const string DeskDoesNotEstimateError =
        "This desk does not estimate its requests. Choose an estimation scale for it first.";

    /// <summary>
    /// The failure for choosing a request that is not an open request on the desk. It names none, since a request
    /// the caller cannot see reads the same as one that does not exist.
    /// </summary>
    public const string NotOpenRequestsOfDeskError = "Choose open requests on this desk.";

    /// <summary>
    /// The desk, when the reader works it and it estimates its requests.
    /// </summary>
    public static async Task<Result<IntakeDesk>> FindDeskToEstimateOn(this IDemandManagementDbContext dbContext,
        IntakeRequestReader reader, Guid deskId, CancellationToken cancellationToken)
    {
        var desk = await dbContext.IntakeDesks.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deskId, cancellationToken);
        if (desk is null)
            return Result.Failure<IntakeDesk>(IntakeDeskAccess.NotFoundError);

        if (!reader.IsAgentOn(desk.Id))
            return Result.Failure<IntakeDesk>(NotAnAgentError);

        return desk.EstimationScaleVersionId is null
            ? Result.Failure<IntakeDesk>(DeskDoesNotEstimateError)
            : desk;
    }

    /// <summary>
    /// The requests as round subjects, in the order given: each one's key, title and description, which the agent
    /// adding them shares with the session's participants whatever the desk's visibility. Refused as a whole unless
    /// every request is open and on the desk.
    /// </summary>
    public static async Task<Result<IReadOnlyList<PokerRoundSubjectInput>>> PokerRoundSubjects(this IDemandManagementDbContext dbContext,
        Guid deskId, IReadOnlyList<Guid> requestIds, CancellationToken cancellationToken)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Success<IReadOnlyList<PokerRoundSubjectInput>>([]);

        var requests = await dbContext.IntakeRequests
            .Where(r => ids.Contains(r.Id) && r.DeskId == deskId)
            .Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active)
            .Select(r => new { r.Id, r.Key, r.Title, r.Description })
            .ToListAsync(cancellationToken);

        if (requests.Count != ids.Count)
            return Result.Failure<IReadOnlyList<PokerRoundSubjectInput>>(NotOpenRequestsOfDeskError);

        var byId = requests.ToDictionary(r => r.Id);
        return Result.Success<IReadOnlyList<PokerRoundSubjectInput>>([.. ids
            .Select(id => byId[id])
            .Select(r => new PokerRoundSubjectInput(PokerRoundSubjectKind.IntakeRequest, r.Id, r.Key.ToString(), r.Title, r.Description))]);
    }
}
