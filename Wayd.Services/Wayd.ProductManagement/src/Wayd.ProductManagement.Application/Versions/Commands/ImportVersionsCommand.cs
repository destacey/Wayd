using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Application.Versions.Imports;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Submits a file of versions to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportVersionsCommand(
    IReadOnlyList<SubmittedImportRow<ImportVersionDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A product may legitimately hold many versions, so only an exact (product, number) pair repeating is a
/// duplicate — and a repeat could never apply whole, since the second would clash with the first.
/// </remarks>
public sealed class ImportVersionsCommandValidator : AbstractValidator<ImportVersionsCommand>
{
    public ImportVersionsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows
                .Select(r => $"{r.Data.ProductId}|{r.Data.Number.Trim()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == rows.Count)
                .WithMessage("Each product's version numbers must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportVersionDtoValidator()));
    }
}

public sealed class ImportVersionsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportVersionsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportVersionsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no versions.");

        var definition = _registry.Find(VersionImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(VersionImportDefinition.ImportKey, rows, command.SubmissionGroupId), cancellationToken);
    }
}
