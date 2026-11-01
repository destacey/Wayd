using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Extensions;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.Web.Api.Models.ProductManagement;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// A single CSV row for the intake request import: one request, as it was submitted and where its review
/// has got to. Desks, channels, tags and people are named by id. A request starts in the status meaning
/// Submitted and is moved to <c>Status</c> when the row names another, recorded at <c>StatusChangedAt</c>.
/// Timestamps are instants, and each must carry its offset.
/// </summary>
public sealed class ImportIntakeRequestRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Leave a cell blank and the row is known by its position instead, but the
    /// column itself must be in the file.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>What is being asked for, in a line. Max 256 chars.</summary>
    public string Title { get; set; } = default!;

    /// <summary>The ask in full.</summary>
    public string Description { get; set; } = default!;

    /// <summary>What gets better, and for whom, if the ask is delivered.</summary>
    public string? ExpectedBenefits { get; set; }

    /// <summary>The desk the request is on, by id. It must be open; it need not be one requesters can pick.</summary>
    public Guid DeskId { get; set; }

    /// <summary>Where the request came from, by id: any active channel, whether or not the desk offers it.</summary>
    public Guid? ChannelId { get; set; }

    /// <summary>The employee the request is for, by id.</summary>
    public Guid RequesterId { get; set; }

    /// <summary>The employee who entered it, by id, when not the requester.</summary>
    public Guid? SubmittedById { get; set; }

    /// <summary>When it was submitted, with its offset. Blank for the moment it is imported.</summary>
    public string? SubmittedAt { get; set; }

    /// <summary>The date it is needed by. Needs a basis and a reference.</summary>
    public DateOnly? NeedByDate { get; set; }

    /// <summary>What makes it that date, case-insensitively. Required with a need-by date.</summary>
    [CsvValues(typeof(NeedByBasis))]
    public string? NeedByBasis { get; set; }

    /// <summary>What the date rests on: the regulation and clause, the contract and clause, the event, or a note. Max 1024 chars.</summary>
    public string? NeedByReference { get; set; }

    /// <summary>For a Commitment, the employee who made it, by id. Required for a Commitment, and empty otherwise.</summary>
    public Guid? NeedByCommittedById { get; set; }

    /// <summary>For a Commitment, the date it was made. Required for a Commitment, and empty otherwise.</summary>
    public DateOnly? NeedByCommittedOn { get; set; }

    /// <summary>The system the request was raised in. Required with an external id.</summary>
    public string? OriginSourceSystem { get; set; }

    /// <summary>The request's id in that system. With the source system, identifies at most one request.</summary>
    public string? OriginExternalId { get; set; }

    /// <summary>A link to the request in that system.</summary>
    public string? OriginUrl { get; set; }

    /// <summary>
    /// A status of the desk's workflow, by its name (<c>In Review</c>) or its meaning (<c>Submitted</c>,
    /// <c>WaitingOnRequester</c>, <c>Accepted</c>, <c>Fulfilled</c>, <c>Duplicate</c>, <c>Withdrawn</c>),
    /// case-insensitively. Blank leaves the request submitted.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// When it moved into the status, with its offset. Blank for SubmittedAt; needs SubmittedAt, and a Status the
    /// request moves to.
    /// </summary>
    public string? StatusChangedAt { get; set; }

    /// <summary>
    /// Why, as the requester reads it. Required for a closing status other than Withdrawn, and for finishing
    /// one. Max 1024 chars.
    /// </summary>
    public string? StatusReason { get; set; }

    /// <summary>For a status meaning Duplicate, the request already in Wayd this one gives way to, by id.</summary>
    public Guid? DuplicateOfId { get; set; }

    /// <summary>The active member of the desk who owns the request, by id.</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>Semicolon-separated tag ids: tags on axes the desk offers, or free tags where it allows them.</summary>
    public string? TagIds { get; set; }

    public ImportIntakeRequestDto ToImportIntakeRequestDto() =>
        new(Title,
            Description,
            Blank(ExpectedBenefits),
            DeskId,
            ChannelId,
            RequesterId,
            SubmittedById,
            OffsetTimestamp.Parse(SubmittedAt),
            NeedByDate is { } date
                ? new ImportIntakeRequestNeedByDto(
                    date.ToLocalDate(),
                    ParseBasis(NeedByBasis)!.Value,
                    NeedByReference!,
                    NeedByCommittedById,
                    NeedByCommittedOn?.ToLocalDate())
                : null,
            string.IsNullOrWhiteSpace(OriginSourceSystem) && string.IsNullOrWhiteSpace(OriginExternalId)
                ? null
                : new ImportIntakeRequestOriginDto(OriginSourceSystem!, OriginExternalId!, Blank(OriginUrl)),
            Blank(Status),
            OffsetTimestamp.Parse(StatusChangedAt),
            Blank(StatusReason),
            DuplicateOfId,
            AssigneeId,
            CsvList.SplitIds(TagIds));

    /// <summary>
    /// Reads a basis by name. Null for a blank cell, and for a value the validator has already refused.
    /// </summary>
    internal static NeedByBasis? ParseBasis(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.GetValues<NeedByBasis>()
                .Where(b => string.Equals(b.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(b => (NeedByBasis?)b)
                .FirstOrDefault();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class ImportIntakeRequestRequestValidator : CustomValidator<ImportIntakeRequestRequest>
{
    public ImportIntakeRequestRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).NotEmpty();
        RuleFor(r => r.DeskId).NotEmpty();
        RuleFor(r => r.RequesterId).NotEmpty();

        RuleFor(r => r.SubmittedAt)
            .Must(OffsetTimestamp.IsValid)
                .When(r => !string.IsNullOrWhiteSpace(r.SubmittedAt))
                .WithMessage(OffsetTimestamp.Message(nameof(ImportIntakeRequestRequest.SubmittedAt)));

        RuleFor(r => r.StatusChangedAt)
            .Must(OffsetTimestamp.IsValid)
                .When(r => !string.IsNullOrWhiteSpace(r.StatusChangedAt))
                .WithMessage(OffsetTimestamp.Message(nameof(ImportIntakeRequestRequest.StatusChangedAt)));

        RuleFor(r => r.NeedByBasis)
            .Must(b => ImportIntakeRequestRequest.ParseBasis(b) is not null)
                .When(r => r.NeedByDate is not null || !string.IsNullOrWhiteSpace(r.NeedByBasis))
                .WithMessage("NeedByBasis must be 'Regulatory', 'Contractual', 'ExternalEvent', 'Commitment' or 'RequesterPreference', and is required with a NeedByDate.");

        RuleFor(r => r.NeedByReference)
            .NotEmpty()
                .When(r => r.NeedByDate is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A NeedByDate needs a NeedByReference saying what the date rests on.")
            .MaximumLength(1024);

        RuleFor(r => r)
            .Must(r => r.NeedByDate is not null
                || (string.IsNullOrWhiteSpace(r.NeedByBasis) && string.IsNullOrWhiteSpace(r.NeedByReference)
                    && r.NeedByCommittedById is null && r.NeedByCommittedOn is null))
                .WithMessage("The NeedBy columns need a NeedByDate.");

        RuleFor(r => r)
            .Must(r => string.IsNullOrWhiteSpace(r.OriginSourceSystem) == string.IsNullOrWhiteSpace(r.OriginExternalId))
                .WithMessage("An origin needs both its OriginSourceSystem and its OriginExternalId.")
            .Must(r => string.IsNullOrWhiteSpace(r.OriginUrl) || !string.IsNullOrWhiteSpace(r.OriginExternalId))
                .WithMessage("An OriginUrl needs an OriginSourceSystem and an OriginExternalId.");

        RuleFor(r => r.TagIds)
            .Must(CsvList.AreAllIds)
                .WithMessage("TagIds must be a semicolon-separated list of ids.");
    }
}
