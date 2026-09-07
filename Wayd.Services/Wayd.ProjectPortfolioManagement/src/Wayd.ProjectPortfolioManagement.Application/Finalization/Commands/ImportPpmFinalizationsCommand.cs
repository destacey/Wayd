using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Imports;

namespace Wayd.ProjectPortfolioManagement.Application.Finalization.Commands;

/// <summary>
/// Submits a file of PPM finalizations to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportPpmFinalizationsCommand(
    IReadOnlyList<SubmittedImportRow<FinalizePpmItemDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// An item can only be finalized once, so two rows for the same one contradict each other — and the second
/// would be refused by the domain anyway, once the first had closed it.
/// </remarks>
public sealed class ImportPpmFinalizationsCommandValidator : CustomValidator<ImportPpmFinalizationsCommand>
{
    public ImportPpmFinalizationsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows.Select(r => r.Data.Id).Distinct().Count() == rows.Count)
                .WithMessage("Each program or portfolio may appear only once within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new FinalizePpmItemDtoValidator()));
    }
}

public sealed class ImportPpmFinalizationsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportPpmFinalizationsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportPpmFinalizationsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no finalizations.");

        var definition = _registry.Find(PpmFinalizationImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(PpmFinalizationImportDefinition.ImportKey, rows), cancellationToken);
    }
}
