namespace Wayd.Web.Api.Models.Ppm.Projects;

/// <summary>A deliverable to add to a project. It starts Not Started, last in the project's order.</summary>
public sealed record CreateProjectDeliverableRequest
{
    /// <summary>What the deliverable is called, at most 256 characters.</summary>
    public string Name { get; set; } = default!;

    /// <summary>What the deliverable is, in Markdown, at most 4096 characters.</summary>
    public string? Description { get; set; }

    /// <summary>How the project will know the deliverable is done, in Markdown, at most 4096 characters.</summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>The employee accountable for the deliverable, by id. Must be active.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>When the deliverable is due.</summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>The project's <c>revision</c> when the change was prepared. A project changed since answers 409.</summary>
    public int? ExpectedRevision { get; set; }
}

public sealed class CreateProjectDeliverableRequestValidator : CustomValidator<CreateProjectDeliverableRequest>
{
    public CreateProjectDeliverableRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).MaximumLength(4096);
        RuleFor(r => r.AcceptanceCriteria).MaximumLength(4096);
    }
}

/// <summary>
/// A deliverable's descriptive fields. Every field is overwritten, so echo back what should stay the same.
/// </summary>
public sealed record UpdateProjectDeliverableRequest
{
    /// <summary>What the deliverable is called, at most 256 characters.</summary>
    public string Name { get; set; } = default!;

    /// <summary>What the deliverable is, in Markdown, at most 4096 characters.</summary>
    public string? Description { get; set; }

    /// <summary>How the project will know the deliverable is done, in Markdown, at most 4096 characters.</summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>The employee accountable for the deliverable, by id, or null for none.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>When the deliverable is due, or null for no date.</summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>The project's <c>revision</c> when the change was prepared. A project changed since answers 409.</summary>
    public int? ExpectedRevision { get; set; }
}

public sealed class UpdateProjectDeliverableRequestValidator : CustomValidator<UpdateProjectDeliverableRequest>
{
    public UpdateProjectDeliverableRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).MaximumLength(4096);
        RuleFor(r => r.AcceptanceCriteria).MaximumLength(4096);
    }
}

/// <summary>The status to move a deliverable to.</summary>
public sealed record ChangeProjectDeliverableStatusRequest
{
    /// <summary>A status of the deliverable workflow, from the deliverable's status options.</summary>
    public Guid StatusId { get; set; }

    /// <summary>Why, at most 1024 characters. Required to descope, or to move to any other Removed status.</summary>
    public string? Reason { get; set; }

    /// <summary>The project's <c>revision</c> when the change was prepared. A project changed since answers 409.</summary>
    public int? ExpectedRevision { get; set; }
}

public sealed class ChangeProjectDeliverableStatusRequestValidator : CustomValidator<ChangeProjectDeliverableStatusRequest>
{
    public ChangeProjectDeliverableStatusRequestValidator()
    {
        RuleFor(r => r.StatusId).NotEmpty();
        RuleFor(r => r.Reason).MaximumLength(1024);
    }
}

/// <summary>A project's deliverables in their new order.</summary>
public sealed record ReorderProjectDeliverablesRequest
{
    /// <summary>Every deliverable of the project, by id, each exactly once, first to last.</summary>
    public List<Guid> DeliverableIds { get; set; } = [];

    /// <summary>The project's <c>revision</c> when the change was prepared. A project changed since answers 409.</summary>
    public int? ExpectedRevision { get; set; }
}

public sealed class ReorderProjectDeliverablesRequestValidator : CustomValidator<ReorderProjectDeliverablesRequest>
{
    public ReorderProjectDeliverablesRequestValidator()
    {
        RuleFor(r => r.DeliverableIds).NotNull();
    }
}
