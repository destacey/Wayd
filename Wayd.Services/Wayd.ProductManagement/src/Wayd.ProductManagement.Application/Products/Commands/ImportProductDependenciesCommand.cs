using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Application.Products.Imports;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Submits a file of product dependencies to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportProductDependenciesCommand(
    IReadOnlyList<SubmittedImportRow<ImportProductDependencyDto>> Rows,
    Guid? SubmissionGroupId = null, bool ValidateOnly = false) : ICommand<Guid>;

public sealed class ImportProductDependenciesCommandValidator : AbstractValidator<ImportProductDependenciesCommand>
{
    public ImportProductDependenciesCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(c => c.Rows)
            .NotEmpty();

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportProductDependencyDtoValidator(dateTimeProvider)));
    }
}

public sealed class ImportProductDependenciesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportProductDependenciesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportProductDependenciesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no dependencies.");

        var definition = _registry.Find(ProductDependencyImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ProductDependencyImportDefinition.ImportKey, rows, command.SubmissionGroupId, command.ValidateOnly), cancellationToken);
    }
}
