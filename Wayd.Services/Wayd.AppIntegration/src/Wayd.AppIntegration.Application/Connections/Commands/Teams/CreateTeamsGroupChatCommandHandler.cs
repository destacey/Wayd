using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces.Communication;
using Wayd.Common.Application.Requests.AppIntegration;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

/// <summary>A group chat topic cannot be empty, and Graph rejects one longer than 250 characters.</summary>
public sealed class CreateTeamsGroupChatCommandValidator : AbstractValidator<CreateTeamsGroupChatCommand>
{
    public const int TopicMaxLength = 250;

    public CreateTeamsGroupChatCommandValidator()
    {
        RuleFor(c => c.Topic).NotEmpty().MaximumLength(TopicMaxLength);
    }
}

/// <summary>
/// Creates one group chat with the only Teams connector that is active and has a valid configuration.
/// Zero or several such connectors fail the call. Optional members who are not in the tenant are left out.
/// A required member who is not in the tenant fails the call, and no chat is created.
/// </summary>
public sealed class CreateTeamsGroupChatCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ITeamsCommunicationGraphClient teamsCommunicationGraphClient,
    ILogger<CreateTeamsGroupChatCommandHandler> logger)
    : ICommandHandler<CreateTeamsGroupChatCommand, TeamsCreatedChat>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ITeamsCommunicationGraphClient _teamsCommunicationGraphClient = teamsCommunicationGraphClient;
    private readonly ILogger<CreateTeamsGroupChatCommandHandler> _logger = logger;

    public async Task<Result<TeamsCreatedChat>> Handle(
        CreateTeamsGroupChatCommand request,
        CancellationToken cancellationToken)
    {
        var connections = await _appIntegrationDbContext.TeamsConnections
            .AsNoTracking()
            .Where(c => c.IsActive && c.IsValidConfiguration && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (connections.Count == 0)
        {
            _logger.LogInformation("No active Teams connector with a valid configuration can create a chat.");
            return Result.Failure<TeamsCreatedChat>(
                "No active Teams connector with a valid configuration can create a chat.");
        }

        if (connections.Count > 1)
        {
            _logger.LogInformation(
                "{ConnectorCount} active Teams connectors have a valid configuration, so no chat was created.",
                connections.Count);
            return Result.Failure<TeamsCreatedChat>(
                "More than one active Teams connector can create a chat. Leave one active.");
        }

        var configuration = connections[0].Configuration;
        var credentials = new TeamsGraphCredentials(
            configuration.TenantId,
            configuration.ClientId,
            configuration.ClientSecret);

        if (HasBlank(request.RequiredMemberEmails))
        {
            return Result.Failure<TeamsCreatedChat>("A person added for this chat has no email address.");
        }

        var optional = DistinctEmails(request.OptionalMemberEmails);
        var required = DistinctEmails(request.RequiredMemberEmails);
        var userIds = new List<string>();

        var optionalMembers = await Resolve(credentials, optional, requiredToExist: false, userIds, cancellationToken);
        if (optionalMembers.IsFailure)
            return Result.Failure<TeamsCreatedChat>(optionalMembers.Error);

        var requiredMembers = await Resolve(credentials, required, requiredToExist: true, userIds, cancellationToken);
        if (requiredMembers.IsFailure)
            return Result.Failure<TeamsCreatedChat>(requiredMembers.Error);

        var members = userIds.Distinct(StringComparer.Ordinal).ToList();
        if (members.Count == 0)
        {
            return Result.Failure<TeamsCreatedChat>(
                "None of the incident's people could be found in the Teams tenant, so the chat was not created.");
        }

        return await _teamsCommunicationGraphClient.CreateGroupChatAsync(
            credentials,
            request.Topic.Trim(),
            members,
            cancellationToken);
    }

    private async Task<Result> Resolve(
        TeamsGraphCredentials credentials,
        IReadOnlyList<string> emails,
        bool requiredToExist,
        List<string> userIds,
        CancellationToken cancellationToken)
    {
        foreach (var email in emails)
        {
            var found = await _teamsCommunicationGraphClient.FindUserIdByEmailAsync(
                credentials,
                email,
                cancellationToken);
            if (found.IsFailure)
                return Result.Failure(found.Error);

            if (found.Value is null)
            {
                if (requiredToExist)
                    return Result.Failure($"No Teams user was found for {email}.");

                continue;
            }

            userIds.Add(found.Value);
        }

        return Result.Success();
    }

    private static bool HasBlank(IReadOnlyList<string>? emails) =>
        emails?.Any(string.IsNullOrWhiteSpace) == true;

    private static List<string> DistinctEmails(IReadOnlyList<string>? emails) =>
        (emails ?? [])
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
