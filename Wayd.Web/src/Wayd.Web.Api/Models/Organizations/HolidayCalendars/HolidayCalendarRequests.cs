using Wayd.Organization.Application.HolidayCalendars.Commands;
using Wayd.Organization.Domain.Models;

namespace Wayd.Web.Api.Models.Organizations.HolidayCalendars;

public sealed record CreateHolidayCalendarRequest
{
    /// <summary>The calendar's name, unique across calendars, such as "United States".</summary>
    public string Name { get; set; } = default!;

    /// <summary>What the calendar covers, such as the region or office.</summary>
    public string? Description { get; set; }

    public CreateHolidayCalendarCommand ToCreateHolidayCalendarCommand() => new(Name, Description);
}

public sealed class CreateHolidayCalendarRequestValidator : CustomValidator<CreateHolidayCalendarRequest>
{
    public CreateHolidayCalendarRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.NameMaxLength);

        RuleFor(r => r.Description)
            .MaximumLength(HolidayCalendar.DescriptionMaxLength);
    }
}

public sealed record UpdateHolidayCalendarRequest
{
    /// <summary>The calendar's id, which must match the route.</summary>
    public Guid Id { get; set; }

    /// <summary>The calendar's name, unique across calendars.</summary>
    public string Name { get; set; } = default!;

    /// <summary>What the calendar covers, such as the region or office.</summary>
    public string? Description { get; set; }

    public UpdateHolidayCalendarCommand ToUpdateHolidayCalendarCommand() => new(Id, Name, Description);
}

public sealed class UpdateHolidayCalendarRequestValidator : CustomValidator<UpdateHolidayCalendarRequest>
{
    public UpdateHolidayCalendarRequestValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.NameMaxLength);

        RuleFor(r => r.Description)
            .MaximumLength(HolidayCalendar.DescriptionMaxLength);
    }
}

public sealed record HolidayRequest
{
    /// <summary>The day off. A calendar holds at most one holiday per date.</summary>
    public LocalDate Date { get; set; }

    /// <summary>What the day is, such as "New Year's Day".</summary>
    public string Name { get; set; } = default!;
}

public sealed class HolidayRequestValidator : CustomValidator<HolidayRequest>
{
    public HolidayRequestValidator()
    {
        RuleFor(r => r.Date)
            .NotEmpty();

        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(HolidayCalendar.HolidayNameMaxLength);
    }
}
