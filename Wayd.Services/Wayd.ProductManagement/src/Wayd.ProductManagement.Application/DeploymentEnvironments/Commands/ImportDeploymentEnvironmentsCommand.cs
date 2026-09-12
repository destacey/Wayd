using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Imports;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;

/// <summary>
/// Submits a file of deployment environments to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportDeploymentEnvironmentsCommand(
    IReadOnlyList<SubmittedImportRow<ImportDeploymentEnvironmentDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// Environments are resolved by name from the deployments import, so a file repeating a name is ambiguous
/// no matter what already exists. The definition checks each row against what is already there; only this
/// can see the file as a set, so the two checks are complements rather than duplicates.
/// </remarks>
public sealed class ImportDeploymentEnvironmentsCommandValidator : AbstractValidator<ImportDeploymentEnvironmentsCommand>
{
    public ImportDeploymentEnvironmentsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows
                .Select(r => r.Data.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == rows.Count)
                .WithMessage("Environment Name must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportDeploymentEnvironmentDtoValidator()));
    }
}

public sealed class ImportDeploymentEnvironmentsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportDeploymentEnvironmentsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportDeploymentEnvironmentsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no environments.");

        var definition = _registry.Find(DeploymentEnvironmentImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(DeploymentEnvironmentImportDefinition.ImportKey, rows, command.SubmissionGroupId),
            cancellationToken);
    }
}
