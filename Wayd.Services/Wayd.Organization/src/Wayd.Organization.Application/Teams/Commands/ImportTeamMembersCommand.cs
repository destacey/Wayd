using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Imports;

namespace Wayd.Organization.Application.Teams.Commands;

/// <summary>
/// Submits a file of team staffing rows to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportTeamMembersCommand(
    IReadOnlyList<SubmittedImportRow<ImportTeamMemberDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, not the file they arrived in.
/// </summary>
/// <remarks>
/// On the handler pipeline, so the check holds for any caller rather than only the one endpoint that
/// happens to validate its request model first.
/// </remarks>
public sealed class ImportTeamMembersCommandValidator : CustomValidator<ImportTeamMembersCommand>
{
    public ImportTeamMembersCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty();

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportTeamMemberDtoValidator()));
    }
}

public sealed class ImportTeamMembersCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportTeamMembersCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportTeamMembersCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no team members.");

        var definition = _registry.Find(TeamMemberImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(TeamMemberImportDefinition.ImportKey, rows, command.SubmissionGroupId), cancellationToken);
    }
}
