using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Application.Releases.Imports;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Submits a file of releases to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the release file and the optional
/// contents file, checks the permission, validates each row's shape, groups the content rows onto the
/// release row they name, then hands the parsed rows here.
/// </remarks>
public sealed record ImportReleasesCommand(
    IReadOnlyList<SubmittedImportRow<ImportReleaseDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A release is identified by its version, so a file repeating one could never apply whole — the second
/// would clash with the first.
/// </remarks>
public sealed class ImportReleasesCommandValidator : AbstractValidator<ImportReleasesCommand>
{
    public ImportReleasesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows.Select(r => r.Data.Version.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count)
                .WithMessage("Release Version must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportReleaseDtoValidator()));
    }
}

public sealed class ImportReleasesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportReleasesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportReleasesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no releases.");

        var definition = _registry.Find(ReleaseImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ReleaseImportDefinition.ImportKey, rows), cancellationToken);
    }
}
