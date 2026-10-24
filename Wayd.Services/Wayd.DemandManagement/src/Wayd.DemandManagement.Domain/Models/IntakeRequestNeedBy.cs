using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// When an intake request has to land, and why: the date, its basis, and the reference the date rests on.
/// </summary>
/// <remarks>
/// A date never travels alone, because a date tells a reviewer nothing without what makes it that date.
/// The parts change together and a request either has all of them or none, so they are one value rather
/// than nullable fields on the request that could disagree.
/// </remarks>
public sealed record IntakeRequestNeedBy
{
    private IntakeRequestNeedBy() { }

    private IntakeRequestNeedBy(LocalDate date, NeedByBasis basis, string reference, Guid? committedById, LocalDate? committedOn)
    {
        Date = date;
        Basis = basis;
        Reference = reference;
        CommittedById = committedById;
        CommittedOn = committedOn;
    }

    /// <summary>The date the request is needed by.</summary>
    public LocalDate Date { get; private init; }

    /// <summary>What makes it that date.</summary>
    public NeedByBasis Basis { get; private init; }

    /// <summary>
    /// What the date rests on: the regulation and clause, the contract and clause, the event, or a note.
    /// </summary>
    public string Reference { get; private init; } = default!;

    /// <summary>For a commitment, the employee who made it; otherwise null.</summary>
    public Guid? CommittedById { get; private init; }

    /// <summary>For a commitment, the date it was made; otherwise null.</summary>
    /// <remarks>
    /// A calendar date rather than an instant: the commitment was made in a meeting and is often recorded
    /// days later, and the moment it was recorded is already on the event that recorded it.
    /// </remarks>
    public LocalDate? CommittedOn { get; private init; }

    /// <summary>
    /// Builds a need-by, refusing one whose parts do not belong together.
    /// </summary>
    /// <remarks>
    /// Every basis needs a reference. A commitment additionally needs who made it and when, because a
    /// commitment without an owner cannot be renegotiated, only missed; any other basis must carry neither.
    /// </remarks>
    public static Result<IntakeRequestNeedBy> Create(
        LocalDate date,
        NeedByBasis basis,
        string? reference,
        Guid? committedById,
        LocalDate? committedOn)
    {
        if (date == default)
            return Result.Failure<IntakeRequestNeedBy>("A need-by needs its date.");

        if (!Enum.IsDefined(basis))
            return Result.Failure<IntakeRequestNeedBy>("The need-by basis is not recognized.");

        if (string.IsNullOrWhiteSpace(reference))
            return Result.Failure<IntakeRequestNeedBy>("A need-by date needs a reference saying what the date rests on.");

        if (basis.RecordsCommitment())
        {
            if (committedById is null || committedById == Guid.Empty || committedOn is null || committedOn == default(LocalDate))
                return Result.Failure<IntakeRequestNeedBy>("A commitment needs who made it and the date it was made.");
        }
        else if (committedById is not null || committedOn is not null)
        {
            return Result.Failure<IntakeRequestNeedBy>("Only a commitment records who made it and when.");
        }

        return new IntakeRequestNeedBy(date, basis, reference.Trim(), committedById, committedOn);
    }

    /// <summary>
    /// The need-by as an event records it.
    /// </summary>
    public IntakeRequestNeedByDetails ToDetails() => new(Date, Basis, Reference, CommittedById, CommittedOn);
}
