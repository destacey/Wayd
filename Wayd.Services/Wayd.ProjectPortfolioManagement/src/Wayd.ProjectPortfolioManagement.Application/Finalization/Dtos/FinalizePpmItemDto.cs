namespace Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;

/// <summary>The kind of item a finalization row closes.</summary>
public enum FinalizePpmItemType
{
    Program = 1,
    Portfolio = 2,
}

/// <summary>The terminal status a finalization row drives its item to.</summary>
public enum FinalizePpmItemStatus
{
    /// <summary>Completes a program. Not valid for a portfolio.</summary>
    Completed = 1,

    /// <summary>Cancels a program. Not valid for a portfolio.</summary>
    Cancelled = 2,

    /// <summary>Closes a portfolio. Not valid for a program.</summary>
    Closed = 3,

    /// <summary>Closes and then archives a portfolio. Not valid for a program.</summary>
    Archived = 4,
}

/// <summary>
/// A single finalization row, closing one program or portfolio after its contents have been imported.
/// <para>
/// This exists because the domain forces an order that a single top-down pass cannot satisfy: a program
/// only accepts projects while it is active and a portfolio only accepts programs and projects while it is
/// active, yet a program can only be completed once all its projects are closed and a portfolio only once
/// all its programs and projects are. Historical work therefore has to be imported active and closed here,
/// last.
/// </para>
/// Items are referenced by name — a program by its own name plus its portfolio's, since program names are
/// only unique within a portfolio.
/// </summary>
public sealed record FinalizePpmItemDto(
    FinalizePpmItemType Type,
    string Name,
    string? PortfolioName,
    FinalizePpmItemStatus Status,
    LocalDate? EndDate);

public sealed class FinalizePpmItemDtoValidator : CustomValidator<FinalizePpmItemDto>
{
    public FinalizePpmItemDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Type)
            .IsInEnum();

        RuleFor(i => i.Name)
            .NotEmpty();

        RuleFor(i => i.Status)
            .IsInEnum();

        RuleFor(i => i.PortfolioName)
            .NotEmpty()
            .When(i => i.Type is FinalizePpmItemType.Program)
                .WithMessage("A program row must name the portfolio it belongs to.");

        RuleFor(i => i.Status)
            .Must(s => s is FinalizePpmItemStatus.Completed or FinalizePpmItemStatus.Cancelled)
            .When(i => i.Type is FinalizePpmItemType.Program)
                .WithMessage("A program can only be finalized as 'Completed' or 'Cancelled'.");

        RuleFor(i => i.Status)
            .Must(s => s is FinalizePpmItemStatus.Closed or FinalizePpmItemStatus.Archived)
            .When(i => i.Type is FinalizePpmItemType.Portfolio)
                .WithMessage("A portfolio can only be finalized as 'Closed' or 'Archived'.");

        // Closing a portfolio sets its end date, so the row has to carry it. Programs already hold the date
        // range they were created with, and completing one does not change it.
        RuleFor(i => i.EndDate)
            .NotNull()
            .When(i => i.Type is FinalizePpmItemType.Portfolio)
                .WithMessage("A portfolio row must have an EndDate, which becomes the portfolio's end date.");
    }
}
