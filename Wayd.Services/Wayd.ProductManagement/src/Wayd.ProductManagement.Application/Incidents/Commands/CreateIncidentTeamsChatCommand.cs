using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Employees;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

/// <summary>
/// Creates the incident's one Teams chat through Microsoft Graph and stores it.
/// Reported by, submitted by, and the commander are included when they can be found.
/// <paramref name="ExtraEmployeeIds"/> are members of that chat only and are not stored.
/// </summary>
public sealed record CreateIncidentTeamsChatCommand(Guid Id, IReadOnlyList<Guid>? ExtraEmployeeIds)
    : ICommand, IRequireLinkedEmployee;

public sealed class CreateIncidentTeamsChatCommandValidator : AbstractValidator<CreateIncidentTeamsChatCommand>
{
    public CreateIncidentTeamsChatCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleForEach(c => c.ExtraEmployeeIds).NotEmpty();
    }
}

/// <summary>
/// Creates the incident's one Teams chat and stores the chat Graph returned.
/// Does not call Graph when a chat is already stored.
/// </summary>
public sealed class CreateIncidentTeamsChatCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateIncidentTeamsChatCommandHandler> logger)
    : ICommandHandler<CreateIncidentTeamsChatCommand>
{
    /// <summary>Microsoft Graph rejects a group-chat topic longer than this.</summary>
    internal const int GraphTopicMaxLength = 250;

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateIncidentTeamsChatCommandHandler> _logger = logger;

    public async Task<Result> Handle(CreateIncidentTeamsChatCommand request, CancellationToken cancellationToken)
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
            return Result.Failure("A linked employee is required.");

        if (incident.TeamsChat is not null)
            return Result.Failure("This incident already has a Teams chat.");

        var extraIds = (request.ExtraEmployeeIds ?? []).Distinct().ToList();
        var roleIds = new[]
            {
                incident.ReportedByEmployeeId,
                incident.SubmittedByEmployeeId,
                incident.CommanderEmployeeId,
            }
            .OfType<Guid>()
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var people = await _productManagementDbContext.Employees
            .Where(employee => roleIds.Contains(employee.Id) || extraIds.Contains(employee.Id))
            .Where(employee => !employee.IsDeleted)
            .ToListAsync(cancellationToken);

        if (extraIds.Any(id => people.All(employee => employee.Id != id)))
            return Result.Failure("A person added for this chat was not found.");

        if (extraIds.Any(id => EmailOf(people.First(employee => employee.Id == id)) is null))
            return Result.Failure("A person added for this chat has no email address.");

        var topic = ChatName(incident.Key, incident.Title);
        var created = await _dispatcher.Send(
            new CreateTeamsGroupChatCommand(
                topic,
                roleIds.Select(id => EmailOf(people.FirstOrDefault(employee => employee.Id == id))).OfType<string>().ToList(),
                extraIds.Select(id => EmailOf(people.First(employee => employee.Id == id))!).ToList()),
            cancellationToken);

        if (created.IsFailure)
        {
            _logger.LogInformation("Teams chat was not created for incident {IncidentId}.", request.Id);
            return Result.Failure(created.Error);
        }

        var eventsBefore = incident.DomainEvents.Count;
        var recorded = incident.RecordCreatedTeamsChat(
            created.Value.Id,
            topic,
            created.Value.Link,
            EventActor.User(_currentUser.GetUserId(), employeeId.Value),
            _dateTimeProvider.Now);

        if (recorded.IsFailure)
        {
            _logger.LogWarning(
                "Teams chat {ChatId} was created but was not stored on incident {IncidentId}.",
                created.Value.Id,
                request.Id);
            return recorded;
        }

        if (incident.DomainEvents.Count > eventsBefore)
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>The chat name Graph receives: the incident key, then the incident name, cut to the topic limit.</summary>
    internal static string ChatName(int key, string title)
    {
        var name = $"INC-{key} {title}".Trim();
        return name.Length <= GraphTopicMaxLength ? name : name[..GraphTopicMaxLength].TrimEnd();
    }

    private static string? EmailOf(Employee? employee)
    {
        var email = employee?.Email?.Value;
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }
}
