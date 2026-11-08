namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The Request Desk CSV row, as the intake request import consumes it. Column names must match
// ImportIntakeRequestRequest in Wayd.Web.Api/Models/DemandManagement, and every column has to be present even
// when the seed leaves it empty: a missing header fails the whole file.

/// <summary>One row of the intake requests CSV. Desks, channels, tags and people are ids.</summary>
public sealed class IntakeRequestCsvRow
{
    public required string ImportId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? ExpectedBenefits { get; init; }
    public required Guid DeskId { get; init; }
    public Guid? ChannelId { get; init; }
    public required Guid RequesterId { get; init; }
    public Guid? SubmittedById { get; init; }

    /// <summary>An instant with its offset; see <see cref="DeploymentCsvRow.Timestamp"/>.</summary>
    public required string SubmittedAt { get; init; }

    public DateOnly? NeedByDate { get; init; }
    public string? NeedByBasis { get; init; }
    public string? NeedByReference { get; init; }
    public Guid? NeedByCommittedById { get; init; }
    public DateOnly? NeedByCommittedOn { get; init; }
    public string? OriginSourceSystem { get; init; }
    public string? OriginExternalId { get; init; }
    public string? OriginUrl { get; init; }
    public string? Status { get; init; }
    public string? StatusChangedAt { get; init; }
    public string? StatusReason { get; init; }
    public Guid? DuplicateOfId { get; init; }
    public Guid? AssigneeId { get; init; }

    /// <summary>Semicolon-separated tag ids.</summary>
    public string? TagIds { get; init; }

    /// <summary>An estimate value of the desk's scale, such as <c>M</c>.</summary>
    public string? Estimate { get; init; }
}

/// <summary>
/// One row of the intake request outcomes CSV. Column names must match ImportIntakeRequestOutcomeRequest, and
/// every column has to be present even when the seed leaves it empty.
/// </summary>
public sealed class IntakeRequestOutcomeCsvRow
{
    public required string ImportId { get; init; }
    public required Guid RequestId { get; init; }

    /// <summary><c>NewProject</c>, <c>ExistingProjectScope</c> or <c>WorkItem</c>.</summary>
    public required string Kind { get; init; }

    public Guid? ProjectId { get; init; }
    public Guid? WorkItemId { get; init; }
    public string? Note { get; init; }

    /// <summary>Semicolon-separated deliverable ids of the row's project.</summary>
    public string? DeliverableIds { get; init; }

    public Guid? AddedById { get; init; }

    /// <summary>An instant with its offset; see <see cref="DeploymentCsvRow.Timestamp"/>.</summary>
    public string? AddedOn { get; init; }
}
