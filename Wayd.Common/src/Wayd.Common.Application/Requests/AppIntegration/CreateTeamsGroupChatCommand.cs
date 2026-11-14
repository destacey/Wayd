using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.Common.Application.Requests.AppIntegration;

/// <summary>
/// Creates one Teams group chat through the single active Teams connector whose configuration is valid.
/// Optional members are skipped when the tenant has no matching user. Required members fail the call
/// when they cannot be resolved. Neither list is stored by this command.
/// </summary>
public sealed record CreateTeamsGroupChatCommand(
    string Topic,
    IReadOnlyList<string> OptionalMemberEmails,
    IReadOnlyList<string> RequiredMemberEmails) : ICommand<TeamsCreatedChat>;
