using CsvHelper;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

// The rows of the Request Desk files `generate` writes for inspection. The models carry their people and tags as
// lists, which a CSV cell cannot hold, so each row flattens them into one semicolon-separated column.

/// <summary>A line of <c>intake-desks.csv</c>: a desk and its members as <c>employeeNumber:role</c> pairs.</summary>
[CsvRecord]
public sealed partial class IntakeDeskFileRow
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Visibility { get; init; }
    public required string Members { get; init; }

    public static IntakeDeskFileRow From(IntakeDeskModel desk) => new()
    {
        Name = desk.Name,
        Description = desk.Description,
        Visibility = desk.Visibility,
        Members = string.Join(';', desk.Members.Select(m => $"{m.EmployeeNumber}:{m.Role}")),
    };
}

/// <summary>A line of <c>intake-requests.csv</c>: a request with its tag handles in one column.</summary>
[CsvRecord]
public sealed partial class IntakeRequestFileRow
{
    public required string Handle { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? ExpectedBenefits { get; init; }
    public required string DeskName { get; init; }
    public required string ChannelName { get; init; }
    public required string RequesterEmployeeNumber { get; init; }

    [CsvFormat(CsvFormats.Instant)]
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

    [CsvFormat(CsvFormats.Instant)]
    public DateTimeOffset? StatusChangedAt { get; init; }

    public string? StatusReason { get; init; }
    public string? AssigneeEmployeeNumber { get; init; }
    public required string Tags { get; init; }

    public static IntakeRequestFileRow From(IntakeRequestModel request) => new()
    {
        Handle = request.Handle,
        Title = request.Title,
        Description = request.Description,
        ExpectedBenefits = request.ExpectedBenefits,
        DeskName = request.DeskName,
        ChannelName = request.ChannelName,
        RequesterEmployeeNumber = request.RequesterEmployeeNumber,
        SubmittedAt = request.SubmittedAt,
        NeedByDate = request.NeedByDate,
        NeedByBasis = request.NeedByBasis,
        NeedByReference = request.NeedByReference,
        NeedByCommittedByEmployeeNumber = request.NeedByCommittedByEmployeeNumber,
        NeedByCommittedOn = request.NeedByCommittedOn,
        OriginSourceSystem = request.OriginSourceSystem,
        OriginExternalId = request.OriginExternalId,
        OriginUrl = request.OriginUrl,
        Status = request.Status,
        StatusChangedAt = request.StatusChangedAt,
        StatusReason = request.StatusReason,
        AssigneeEmployeeNumber = request.AssigneeEmployeeNumber,
        Tags = string.Join(';', request.Tags),
    };
}

/// <summary>A line of <c>intake-request-outcomes.csv</c>: an outcome with its deliverable names in one column.</summary>
[CsvRecord]
public sealed partial class IntakeRequestOutcomeFileRow
{
    public required string RequestHandle { get; init; }
    public required string Kind { get; init; }
    public required string ProjectKey { get; init; }
    public string? Note { get; init; }
    public required string Deliverables { get; init; }
    public required string AddedByEmployeeNumber { get; init; }

    [CsvFormat(CsvFormats.Instant)]
    public required DateTimeOffset AddedOn { get; init; }

    public static IntakeRequestOutcomeFileRow From(IntakeRequestOutcomeModel outcome) => new()
    {
        RequestHandle = outcome.RequestHandle,
        Kind = outcome.Kind,
        ProjectKey = outcome.ProjectKey,
        Note = outcome.Note,
        Deliverables = string.Join(';', outcome.DeliverableNames),
        AddedByEmployeeNumber = outcome.AddedByEmployeeNumber,
        AddedOn = outcome.AddedOn,
    };
}
