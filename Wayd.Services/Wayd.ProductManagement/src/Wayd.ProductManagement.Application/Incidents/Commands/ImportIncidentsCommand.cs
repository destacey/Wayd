using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Application.Incidents.Imports;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

/// <summary>
/// Submits a file of incidents to import, and answers with the id of the run.
/// </summary>
public sealed record ImportIncidentsCommand(
    IReadOnlyList<SubmittedImportRow<ImportIncidentDto>> Rows,
    Guid? SubmissionGroupId = null,
    bool ValidateOnly = false) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves. A repeated <c>ImportId</c> is the runner's check, and an incident has
/// no natural key, so nothing else about the file as a whole is a mistake.
/// </summary>
public sealed class ImportIncidentsCommandValidator : AbstractValidator<ImportIncidentsCommand>
{
    public ImportIncidentsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty();

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportIncidentDtoValidator()));
    }
}

/// <summary>Hands a parsed incident file to the import runner.</summary>
public sealed class ImportIncidentsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportIncidentsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportIncidentsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
        {
            return Result.Failure<Guid>("The file contains no incidents.");
        }

        var definition = _registry.Find(IncidentImportDefinition.ImportKey);
        if (definition.IsFailure)
        {
            return Result.Failure<Guid>(definition.Error);
        }

        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(IncidentImportDefinition.ImportKey, rows, command.SubmissionGroupId, command.ValidateOnly),
            cancellationToken);
    }
}
