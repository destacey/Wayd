using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Imports;

namespace Wayd.Planning.Application.Risks.Commands;

/// <summary>
/// Submits a file of risks to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here; knowing which definition applies them and
/// how a payload is stored belongs on this side of the line.
/// <para>
/// It is also where anything this import needs to be true of a whole file goes — a rule about what the
/// file may span, or an authorization question the permission claim alone cannot answer. Risks have none
/// today, which is why this handler is thin; it is the seam that matters, not its current contents.
/// </para>
/// </remarks>
public sealed record ImportRisksCommand(IReadOnlyList<SubmittedImportRow<ImportRiskDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, not the file they arrived in.
/// </summary>
/// <remarks>
/// On the handler pipeline, so the check holds for any caller rather than only the one endpoint that
/// happens to validate its request model first. A controller's validator covers the CSV shape it parsed;
/// this covers what the import is actually being asked to apply.
/// </remarks>
public sealed class ImportRisksCommandValidator : CustomValidator<ImportRisksCommand>
{
    public ImportRisksCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty();

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportRiskDtoValidator(dateTimeProvider)));
    }
}

public sealed class ImportRisksCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportRisksCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportRisksCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no risks.");

        var definition = _registry.Find(RiskImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(RiskImportDefinition.ImportKey, rows), cancellationToken);
    }
}
