namespace Wayd.Web.Api.Models.UserManagement.Profiles;

/// <summary>
/// The signed-in user's home page arrangement, replacing the one saved. Send no widgets to clear it and use
/// the default again. Widget ids are the client's; the server checks only the layout's shape.
/// </summary>
public sealed record UpdateHomeLayoutRequest
{
    /// <summary>The most widgets a layout holds; far more than the home page offers, so it bounds the stored document only.</summary>
    public const int MaxWidgets = 50;

    /// <summary>The longest widget id accepted.</summary>
    public const int MaxWidgetIdLength = 64;

    /// <summary>The placed widgets, in the order they show within their columns.</summary>
    public List<HomeLayoutWidgetRequest> Widgets { get; set; } = [];

    /// <summary>The layout as the user service stores it.</summary>
    public UserHomeLayoutDto ToUserHomeLayoutDto() => new()
    {
        Widgets = [.. Widgets.Select(w => new UserHomeLayoutWidgetDto
        {
            WidgetId = w.WidgetId,
            Column = w.Column,
            Visible = w.Visible,
        })],
    };
}

/// <summary>Where one widget sits on the home page, and whether it shows.</summary>
public sealed record HomeLayoutWidgetRequest
{
    /// <summary>The widget's id in the client's widget registry.</summary>
    public string WidgetId { get; set; } = default!;

    /// <summary>The column the widget sits in.</summary>
    public HomeLayoutColumn Column { get; set; }

    /// <summary>Whether the widget shows; false when the user turned it off.</summary>
    public bool Visible { get; set; } = true;
}

/// <summary>Checks a home layout's shape: how many widgets, each id's length, and each column.</summary>
public sealed class UpdateHomeLayoutRequestValidator : CustomValidator<UpdateHomeLayoutRequest>
{
    /// <summary>Declares the shape rules.</summary>
    public UpdateHomeLayoutRequestValidator()
    {
        // Stop: the count and child rules would dereference a null list or entry.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Widgets)
            .NotNull()
            .Must(w => w.Count <= UpdateHomeLayoutRequest.MaxWidgets)
                .WithMessage($"A home layout holds at most {UpdateHomeLayoutRequest.MaxWidgets} widgets.");

        // ChildRules skips null elements, so a null entry needs its own rule.
        RuleForEach(r => r.Widgets).NotNull().ChildRules(widget =>
        {
            widget.RuleFor(w => w.WidgetId)
                .NotEmpty()
                .MaximumLength(UpdateHomeLayoutRequest.MaxWidgetIdLength);

            widget.RuleFor(w => w.Column)
                .IsInEnum();
        });
    }
}
