using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// One intake request on its way in: the ask as it was submitted, and where its review has got to.
/// </summary>
/// <remarks>
/// Every reference is an id, because desk, channel, tag and employee names are not what a company keys
/// them by. Carries no import id: the platform holds the caller's key on the row rather than in the
/// payload, so results are reported against that.
/// </remarks>
/// <param name="Title">What is being asked for, in a line.</param>
/// <param name="Description">The ask in full.</param>
/// <param name="ExpectedBenefits">What gets better, and for whom, if the ask is delivered.</param>
/// <param name="DeskId">The desk the request is on. Need not be one requesters can pick, but must be open.</param>
/// <param name="ChannelId">
/// Where the request came from: any active company channel, whether or not the desk offers it, as for a
/// request from an upstream system.
/// </param>
/// <param name="RequesterId">The employee the request is for.</param>
/// <param name="SubmittedById">Who entered it, when not the requester.</param>
/// <param name="SubmittedAt">When it was submitted; null for the moment it is imported.</param>
/// <param name="NeedBy">When it has to land, and why.</param>
/// <param name="Origin">The upstream record it arrived from.</param>
/// <param name="Status">
/// A status of the desk's workflow, by its name or by its meaning (<see cref="IntakeRequestStatusAlias"/>);
/// null leaves the request where every request starts.
/// </param>
/// <param name="StatusChangedAt">When it moved into <paramref name="Status"/>; null for <paramref name="SubmittedAt"/>.</param>
/// <param name="StatusReason">Why, as the requester reads it. Required by the statuses that need one.</param>
/// <param name="DuplicateOfId">For a status meaning Duplicate, the request already in Wayd it gives way to.</param>
/// <param name="AssigneeId">The member of the desk who owns it.</param>
/// <param name="TagIds">Tags along axes the desk offers, or free tags where the desk allows them.</param>
public sealed record ImportIntakeRequestDto(
    string Title,
    string Description,
    string? ExpectedBenefits,
    Guid DeskId,
    Guid? ChannelId,
    Guid RequesterId,
    Guid? SubmittedById,
    Instant? SubmittedAt,
    ImportIntakeRequestNeedByDto? NeedBy,
    ImportIntakeRequestOriginDto? Origin,
    string? Status,
    Instant? StatusChangedAt,
    string? StatusReason,
    Guid? DuplicateOfId,
    Guid? AssigneeId,
    IReadOnlyList<Guid> TagIds);

/// <summary>A need-by date with its basis and reference, as an import row carries it.</summary>
/// <param name="Date">The date the request is needed by.</param>
/// <param name="Basis">What makes it that date.</param>
/// <param name="Reference">What the date rests on.</param>
/// <param name="CommittedById">For a commitment, the employee who made it.</param>
/// <param name="CommittedOn">For a commitment, the date it was made.</param>
public sealed record ImportIntakeRequestNeedByDto(
    LocalDate Date,
    NeedByBasis Basis,
    string Reference,
    Guid? CommittedById,
    LocalDate? CommittedOn);

/// <summary>The upstream record an imported request arrived from.</summary>
/// <param name="SourceSystem">The system it was raised in.</param>
/// <param name="ExternalId">Its id there. With the source system, identifies at most one request.</param>
/// <param name="Url">A link to it there.</param>
public sealed record ImportIntakeRequestOriginDto(string SourceSystem, string ExternalId, string? Url);

public sealed class ImportIntakeRequestDtoValidator : CustomValidator<ImportIntakeRequestDto>
{
    public ImportIntakeRequestDtoValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).NotEmpty();
        RuleFor(r => r.DeskId).NotEmpty();
        RuleFor(r => r.ChannelId).NotEqual(Guid.Empty);
        RuleFor(r => r.RequesterId).NotEmpty();
        RuleFor(r => r.SubmittedById).NotEqual(Guid.Empty);
        RuleFor(r => r.AssigneeId).NotEqual(Guid.Empty);
        RuleFor(r => r.DuplicateOfId).NotEqual(Guid.Empty);
        RuleFor(r => r.Status).MaximumLength(64);
        RuleFor(r => r.StatusReason).MaximumLength(1024);
        RuleFor(r => r.TagIds).NotNull();
        RuleForEach(r => r.TagIds).NotEmpty();

        RuleFor(r => r.SubmittedAt)
            .Must(at => at <= dateTimeProvider.Now)
                .When(r => r.SubmittedAt is not null)
                .WithMessage("A request cannot be submitted in the future.");

        RuleFor(r => r.StatusChangedAt)
            .Empty()
                .When(r => string.IsNullOrWhiteSpace(r.Status), ApplyConditionTo.CurrentValidator)
                .WithMessage("StatusChangedAt needs a Status.")
            .Empty()
                .When(r => r.SubmittedAt is null, ApplyConditionTo.CurrentValidator)
                .WithMessage("StatusChangedAt needs a SubmittedAt, so the status cannot change before the request arrived.")
            .Must(at => at <= dateTimeProvider.Now)
                .When(r => r.StatusChangedAt is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A request cannot change status in the future.")
            .Must((r, at) => r.SubmittedAt is null || at >= r.SubmittedAt)
                .When(r => r.StatusChangedAt is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A request cannot change status before it was submitted.");

        When(r => r.NeedBy is not null, () =>
        {
            RuleFor(r => r.NeedBy!.Date).NotEqual(default(LocalDate));
            RuleFor(r => r.NeedBy!.Basis).IsInEnum();
            RuleFor(r => r.NeedBy!.Reference).NotEmpty().MaximumLength(1024);
            RuleFor(r => r.NeedBy!.CommittedById).NotEqual(Guid.Empty);
        });

        When(r => r.Origin is not null, () =>
        {
            RuleFor(r => r.Origin!.SourceSystem).NotEmpty().MaximumLength(128);
            RuleFor(r => r.Origin!.ExternalId).NotEmpty().MaximumLength(256);
            RuleFor(r => r.Origin!.Url).MaximumLength(2048);
        });
    }
}
