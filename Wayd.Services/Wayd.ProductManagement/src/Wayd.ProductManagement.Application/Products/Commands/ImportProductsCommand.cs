using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Application.Products.Imports;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Submits a file of products to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportProductsCommand(
    IReadOnlyList<SubmittedImportRow<ImportProductDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the two things that are true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A parent reference must name a row in this file, and a parent chain that loops back on itself can never
/// be ordered parents-first. Both are properties of the file as a set, which the definition applies rows
/// one at a time against.
/// </remarks>
public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(AllParentsPresent)
                .WithMessage("Every ParentImportId must name another row in the same file. A product already "
                    + "in the catalog cannot be named as a parent here.")
            .Must(NoParentCycles)
                .WithMessage("Some products form a circular parent reference, so they can never be created parents-first.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportProductDtoValidator()));
    }

    private static Dictionary<SubmittedImportRow<ImportProductDto>, string> Keys(
        IReadOnlyList<SubmittedImportRow<ImportProductDto>> rows)
    {
        var keys = new Dictionary<SubmittedImportRow<ImportProductDto>, string>();
        for (var i = 0; i < rows.Count; i++)
            keys[rows[i]] = SubmittedImportRow.KeyFor(rows[i].ImportId, i + 1);

        return keys;
    }

    private static bool AllParentsPresent(IReadOnlyList<SubmittedImportRow<ImportProductDto>> rows)
    {
        var present = Keys(rows).Values.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows.All(r => r.Data.ParentImportId is not { } parent || present.Contains(parent.Trim()));
    }

    /// <summary>
    /// Walks the parent links, peeling off rows whose parent is outside the file or already placed.
    /// Anything left when nothing more can be placed is a cycle.
    /// </summary>
    private static bool NoParentCycles(IReadOnlyList<SubmittedImportRow<ImportProductDto>> rows)
    {
        var keys = Keys(rows);
        var present = keys.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = rows.ToList();

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(r => r.Data.ParentImportId is not { } parent
                    || !present.Contains(parent.Trim())
                    || placed.Contains(parent.Trim()))
                .ToList();

            if (ready.Count == 0)
                return false;

            foreach (var row in ready)
            {
                placed.Add(keys[row]);
                remaining.Remove(row);
            }
        }

        return true;
    }
}

public sealed class ImportProductsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportProductsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportProductsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no products.");

        var definition = _registry.Find(ProductImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ProductImportDefinition.ImportKey, rows), cancellationToken);
    }
}
