namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// A Teams group chat Microsoft Graph created. The link is what opens it.
/// </summary>
public sealed record TeamsCreatedChat(string Id, string Link);
