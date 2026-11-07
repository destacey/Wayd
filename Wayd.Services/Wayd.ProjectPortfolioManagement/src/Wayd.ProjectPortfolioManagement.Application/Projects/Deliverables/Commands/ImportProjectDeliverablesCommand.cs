using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Imports;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>
/// Submits a file of project deliverables to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and validates
/// each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportProjectDeliverablesCommand(
    IReadOnlyList<SubmittedImportRow<ImportProjectDeliverableDto>> Rows,
    Guid? SubmissionGroupId = null, bool ValidateOnly = false) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and that a deliverable's name is unique within its project in the file, which
/// the definition cannot see since it applies rows one at a time.
/// </summary>
public sealed class ImportProjectDeliverablesCommandValidator : CustomValidator<ImportProjectDeliverablesCommand>
{
    public ImportProjectDeliverablesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Custom((rows, context) =>
            {
                var repeated = rows
                    .Where(r => r.Data is not null && !string.IsNullOrWhiteSpace(r.Data.Name))
                    .GroupBy(r => (Project: r.Data.ProjectKey.Value, Name: r.Data.Name.Trim()), DeliverableIdentity.Comparer)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"'{g.Key.Name}' on project {g.Key.Project}")
                    .ToList();

                if (repeated.Count > 0)
                    context.AddFailure(nameof(ImportProjectDeliverablesCommand.Rows),
                        $"A deliverable's Name must be unique within its project in the file, ignoring case. Repeated: {string.Join(", ", repeated)}.");
            });

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportProjectDeliverableDtoValidator()));
    }

    /// <summary>A deliverable's project key and name, compared ignoring case as the domain compares names.</summary>
    private sealed class DeliverableIdentity : IEqualityComparer<(string Project, string Name)>
    {
        public static readonly DeliverableIdentity Comparer = new();

        public bool Equals((string Project, string Name) x, (string Project, string Name) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Project, y.Project) && StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);

        public int GetHashCode((string Project, string Name) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Project), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}

public sealed class ImportProjectDeliverablesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportProjectDeliverablesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportProjectDeliverablesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no project deliverables.");

        var definition = _registry.Find(ProjectDeliverableImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ProjectDeliverableImportDefinition.ImportKey, rows, command.SubmissionGroupId, command.ValidateOnly), cancellationToken);
    }
}
