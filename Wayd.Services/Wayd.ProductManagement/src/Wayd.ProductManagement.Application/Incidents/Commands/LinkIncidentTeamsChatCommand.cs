namespace Wayd.ProductManagement.Application.Incidents.Commands;

/// <summary>Stores a pasted Teams link as the incident's one chat. Wayd does not join it.</summary>
public sealed record LinkIncidentTeamsChatCommand(Guid Id, string Link, string? DisplayName)
    : ICommand, IRequireLinkedEmployee;

public sealed class LinkIncidentTeamsChatCommandValidator : AbstractValidator<LinkIncidentTeamsChatCommand>
{
    public LinkIncidentTeamsChatCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Link)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeHttpsAbsoluteUrl)
            .WithMessage("A Teams chat link must be an https URL.");
        RuleFor(c => c.DisplayName).MaximumLength(256);
    }

    private static bool BeHttpsAbsoluteUrl(string? link)
    {
        var trimmed = link?.Trim();
        return trimmed is not null
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}

public sealed class LinkIncidentTeamsChatCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<LinkIncidentTeamsChatCommandHandler> logger)
    : ICommandHandler<LinkIncidentTeamsChatCommand>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<LinkIncidentTeamsChatCommandHandler> _logger = logger;

    public async Task<Result> Handle(LinkIncidentTeamsChatCommand request, CancellationToken cancellationToken)
    {
        var incident = await _productManagementDbContext.Incidents
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (incident is null)
        {
            _logger.LogInformation("Incident {IncidentId} not found.", request.Id);
            return Result.Failure("Incident not found.");
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Failure("A linked employee is required.");
        }

        var eventsBefore = incident.DomainEvents.Count;
        var linked = incident.LinkTeamsChat(
            request.Link,
            request.DisplayName,
            EventActor.User(_currentUser.GetUserId(), employeeId.Value),
            _dateTimeProvider.Now);

        if (linked.IsFailure)
        {
            return linked;
        }

        if (incident.DomainEvents.Count > eventsBefore)
        {
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
