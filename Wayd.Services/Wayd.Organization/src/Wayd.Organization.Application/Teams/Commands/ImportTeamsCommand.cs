using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Teams.Models;

namespace Wayd.Organization.Application.Teams.Commands;

/// <summary>
/// Submits a file of teams to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportTeamsCommand(IReadOnlyList<SubmittedImportRow<ImportTeamDto>> Rows, Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the two things that are true of a file rather than of any row.
/// </summary>
/// <remarks>
/// Name and code are unique across all teams, so a file repeating either could never apply whole. The
/// definition checks each row against what already exists; only this can see the file as a set, so the
/// two checks are complements rather than duplicates.
/// </remarks>
public sealed class ImportTeamsCommandValidator : CustomValidator<ImportTeamsCommand>
{
    public ImportTeamsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows.Select(r => r.Data.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count)
                .WithMessage("Team Name must be unique within the file.")
            .Must(rows => rows.Select(r => r.Data.Code.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count)
                .WithMessage("Team Code must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportTeamDtoValidator()));
    }
}

public sealed class ImportTeamsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportTeamsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportTeamsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no teams.");

        var definition = _registry.Find(TeamImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(TeamImportDefinition.ImportKey, rows, command.SubmissionGroupId), cancellationToken);
    }
}
