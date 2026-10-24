using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// A need-by date with its basis and reference, as a command receives it.
/// </summary>
/// <param name="Date">The date the request is needed by.</param>
/// <param name="Basis">What makes it that date.</param>
/// <param name="Reference">What the date rests on: the regulation and clause, the contract and clause, the event, or a note.</param>
/// <param name="CommittedById">For a commitment, the employee who made it; otherwise null.</param>
/// <param name="CommittedOn">For a commitment, the date it was made; otherwise null.</param>
public sealed record IntakeRequestNeedByInput(
    LocalDate Date,
    NeedByBasis Basis,
    string Reference,
    Guid? CommittedById,
    LocalDate? CommittedOn)
{
    /// <summary>
    /// Builds the request's need-by, checking that whoever made a newly named commitment is an active employee.
    /// </summary>
    /// <remarks>
    /// A commitment the request already carries is not re-checked: updates resend the whole record, and a
    /// commitment stays a fact about who made it after that person leaves, so re-checking would lock every
    /// later edit of the request.
    /// </remarks>
    /// <param name="dbContext">The module's context.</param>
    /// <param name="currentCommittedById">Who made the commitment the request carries now, when it carries one.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<Result<IntakeRequestNeedBy>> ToNeedBy(
        IDemandManagementDbContext dbContext,
        Guid? currentCommittedById,
        CancellationToken cancellationToken)
    {
        var needBy = IntakeRequestNeedBy.Create(Date, Basis, Reference, CommittedById, CommittedOn);
        if (needBy.IsFailure)
            return needBy;

        if (needBy.Value.CommittedById is { } committedById
            && committedById != currentCommittedById
            && !await dbContext.IsActiveEmployee(committedById, cancellationToken))
            return Result.Failure<IntakeRequestNeedBy>("Whoever made the commitment must be an active employee.");

        return needBy;
    }
}

public sealed class IntakeRequestNeedByInputValidator : CustomValidator<IntakeRequestNeedByInput>
{
    public IntakeRequestNeedByInputValidator()
    {
        RuleFor(n => n.Date).NotEqual(default(LocalDate));
        RuleFor(n => n.Basis).IsInEnum();
        RuleFor(n => n.Reference).NotEmpty().MaximumLength(1024);
        RuleFor(n => n.CommittedById).NotEqual(Guid.Empty);
        RuleFor(n => n.CommittedOn).NotEqual(default(LocalDate));
    }
}
