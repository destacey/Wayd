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

    /// <summary>The most settings one widget holds.</summary>
    public const int MaxWidgetSettings = 16;

    /// <summary>The longest setting key accepted.</summary>
    public const int MaxSettingKeyLength = 64;

    /// <summary>The longest setting value accepted; room for a list of record ids.</summary>
    public const int MaxSettingValueLength = 1024;

    /// <summary>
    /// The most characters a layout's setting keys and values hold between them. The per-widget limits alone
    /// would let one layout approach a megabyte, and the layout is read with every load of its user.
    /// </summary>
    public const int MaxSettingsLength = 16384;

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
            Settings = w.Settings is null ? [] : new Dictionary<string, string>(w.Settings),
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

    /// <summary>
    /// What the user changed about what the widget shows, by setting key; leave out or send empty for a
    /// widget at its defaults. Keys and values are the client's; the server checks only their size.
    /// </summary>
    public Dictionary<string, string>? Settings { get; set; }
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
                .WithMessage($"A home layout holds at most {UpdateHomeLayoutRequest.MaxWidgets} widgets.")
            .Must(w => SettingsLength(w) <= UpdateHomeLayoutRequest.MaxSettingsLength)
                .WithMessage($"A home layout's settings hold at most {UpdateHomeLayoutRequest.MaxSettingsLength} characters in all.");

        // ChildRules skips null elements, so a null entry needs its own rule.
        RuleForEach(r => r.Widgets).NotNull().ChildRules(widget =>
        {
            widget.RuleFor(w => w.WidgetId)
                .NotEmpty()
                .MaximumLength(UpdateHomeLayoutRequest.MaxWidgetIdLength);

            widget.RuleFor(w => w.Column)
                .IsInEnum();

            widget.RuleFor(w => w.Settings)
                .Must(s => s is null || s.Count <= UpdateHomeLayoutRequest.MaxWidgetSettings)
                    .WithMessage($"A widget holds at most {UpdateHomeLayoutRequest.MaxWidgetSettings} settings.")
                .Must(s => s is null || s.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= UpdateHomeLayoutRequest.MaxSettingKeyLength))
                    .WithMessage($"A setting key must not be blank, and is at most {UpdateHomeLayoutRequest.MaxSettingKeyLength} characters.")
                .Must(s => s is null || s.Values.All(v => v is not null && v.Length <= UpdateHomeLayoutRequest.MaxSettingValueLength))
                    .WithMessage($"A setting value must be at most {UpdateHomeLayoutRequest.MaxSettingValueLength} characters.");
        });
    }

    /// <summary>The characters in every setting key and value of a layout; null entries, settings and values count as none.</summary>
    private static int SettingsLength(List<HomeLayoutWidgetRequest> widgets) =>
        widgets.Sum(w => w?.Settings?.Sum(s => s.Key.Length + (s.Value?.Length ?? 0)) ?? 0);
}
