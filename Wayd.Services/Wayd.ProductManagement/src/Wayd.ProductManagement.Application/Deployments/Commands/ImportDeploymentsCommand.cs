using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.ProductManagement.Application.Deployments.Imports;

namespace Wayd.ProductManagement.Application.Deployments.Commands;

/// <summary>
/// Submits a file of deployments to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportDeploymentsCommand(
    IReadOnlyList<SubmittedImportRow<ImportDeploymentDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves.
/// </summary>
/// <remarks>
/// Nothing is true of the file as a whole: a deployment has no natural key — two builds of one version
/// reaching one environment are two deployments — so no repeat within a file is a mistake.
/// </remarks>
public sealed class ImportDeploymentsCommandValidator : AbstractValidator<ImportDeploymentsCommand>
{
    public ImportDeploymentsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty();

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportDeploymentDtoValidator()));
    }
}

public sealed class ImportDeploymentsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportDeploymentsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportDeploymentsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no deployments.");

        var definition = _registry.Find(DeploymentImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(DeploymentImportDefinition.ImportKey, rows, command.SubmissionGroupId), cancellationToken);
    }
}
