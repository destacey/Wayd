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
    /// Builds the request's need-by, checking that whoever made a commitment is an active employee.
    /// </summary>
    internal async Task<Result<IntakeRequestNeedBy>> ToNeedBy(IDemandManagementDbContext dbContext, CancellationToken cancellationToken)
    {
        if (CommittedById is { } committedById && !await dbContext.IsActiveEmployee(committedById, cancellationToken))
            return Result.Failure<IntakeRequestNeedBy>("Whoever made the commitment must be an active employee.");

        return IntakeRequestNeedBy.Create(Date, Basis, Reference, CommittedById, CommittedOn);
    }
}

public sealed class IntakeRequestNeedByInputValidator : CustomValidator<IntakeRequestNeedByInput>
{
    public IntakeRequestNeedByInputValidator()
    {
        RuleFor(n => n.Basis).IsInEnum();
        RuleFor(n => n.Reference).NotEmpty().MaximumLength(1024);
    }
}
