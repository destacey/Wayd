namespace Wayd.Tools.DataGeneration.Cli.Generation;

// The generated Request Desk model, keyed by the generator's own handles — desk names, tag names, employee
// numbers — for the same reason as the other models: ids exist only once the API has created something, so
// the seed areas substitute them as they go.

/// <summary>The generated desks, tag axes and requests.</summary>
public sealed record GeneratedDemandManagement(
    IReadOnlyList<IntakeDeskModel> Desks,
    IReadOnlyList<IntakeTagCategoryModel> TagCategories,
    IReadOnlyList<IntakeRequestModel> Requests);

/// <summary>
/// A generated desk and the people who work it, by employee number. Every member is in post today, since a
/// desk names only active employees.
/// </summary>
public sealed class IntakeDeskModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary><c>Open</c>, <c>SummaryOnly</c> or <c>Private</c>.</summary>
    public required string Visibility { get; init; }

    public required IReadOnlyList<IntakeDeskMemberModel> Members { get; init; }
}

/// <summary>One person on a desk. <see cref="Role"/> is <c>Admin</c> or <c>Agent</c>.</summary>
public sealed record IntakeDeskMemberModel(string EmployeeNumber, string Role);

/// <summary>A tag axis with its tags. A request carries one tag from an axis unless it allows many.</summary>
public sealed class IntakeTagCategoryModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required bool AllowsMany { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}

/// <summary>
/// A generated intake request on the desk named by <see cref="DeskName"/>. People are employee numbers, tags
/// are <c>axis|tag</c> handles, and the channel is one of the seeded channels by name. <see cref="Status"/> is
/// a status of the default workflow by name; a request still Submitted carries no status change.
/// </summary>
public sealed class IntakeRequestModel
{
    public required string Handle { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? ExpectedBenefits { get; init; }
    public required string DeskName { get; init; }
    public required string ChannelName { get; init; }
    public required string RequesterEmployeeNumber { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
    public DateOnly? NeedByDate { get; init; }
    public string? NeedByBasis { get; init; }
    public string? NeedByReference { get; init; }
    public string? NeedByCommittedByEmployeeNumber { get; init; }
    public DateOnly? NeedByCommittedOn { get; init; }
    public string? OriginSourceSystem { get; init; }
    public string? OriginExternalId { get; init; }
    public string? OriginUrl { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? StatusChangedAt { get; init; }
    public string? StatusReason { get; init; }
    public string? AssigneeEmployeeNumber { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}
