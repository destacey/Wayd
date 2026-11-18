using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;

namespace Wayd.Common.Application.Kpis.Imports;

/// <summary>
/// Submits a file of KPIs, each with its history, to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the KPI file and the measurements file,
/// checks the permission, validates each row's shape, groups the measurements onto the KPI row they name,
/// then hands the parsed rows here.
/// </remarks>
public sealed record ImportKpisCommand(
    IReadOnlyList<SubmittedImportRow<ImportKpiDto>> Rows,
    Guid? SubmissionGroupId = null,
    bool ValidateOnly = false) : ICommand<Guid>;

/// <summary>Validates every row's shape before anything is stored.</summary>
public sealed class ImportKpisCommandValidator : AbstractValidator<ImportKpisCommand>
{
    public ImportKpisCommandValidator()
    {
        RuleFor(c => c.Rows).NotEmpty();
        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportKpiDtoValidator()));
    }
}

/// <summary>Stores the rows as the definition serializes them and starts the run.</summary>
public sealed class ImportKpisCommandHandler(IImportDefinitionRegistry registry, IDispatcher dispatcher)
    : ICommandHandler<ImportKpisCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportKpisCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no KPIs.");

        var definition = _registry.Find(KpiImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(KpiImportDefinition.ImportKey, rows, command.SubmissionGroupId, command.ValidateOnly),
            cancellationToken);
    }
}
