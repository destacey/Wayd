using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Application.ReleasePackages.Imports;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// Submits a file of release packages to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the package file and the manifest file,
/// checks the permission, validates each row's shape, groups the manifest lines onto the package row they
/// name, then hands the parsed rows here.
/// </remarks>
public sealed record ImportReleasePackagesCommand(
    IReadOnlyList<SubmittedImportRow<ImportReleasePackageDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the two things that are true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A package is identified by its version, so a file repeating one could never apply whole. A component
/// appearing twice in one manifest is likewise contradictory: a package ships one version of a component,
/// not two.
/// </remarks>
public sealed class ImportReleasePackagesCommandValidator : AbstractValidator<ImportReleasePackagesCommand>
{
    public ImportReleasePackagesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows.Select(r => r.Data.Version.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count)
                .WithMessage("Package Version must be unique within the file.")
            .Must(rows => rows.All(r =>
                r.Data.Components.Select(c => c.ProductId).Distinct().Count() == r.Data.Components.Count))
                .WithMessage("A component may appear only once in a package's manifest.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportReleasePackageDtoValidator()));
    }
}

public sealed class ImportReleasePackagesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportReleasePackagesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportReleasePackagesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no release packages.");

        var definition = _registry.Find(ReleasePackageImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ReleasePackageImportDefinition.ImportKey, rows), cancellationToken);
    }
}
