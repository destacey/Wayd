namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Pure identity: who is the caller (id, name, email, employee link) — nothing about what they may
/// do. Authorization questions belong to <see cref="ICurrentPrincipal"/>; raw claims stay behind
/// the auth infrastructure and are deliberately not exposed here.
/// </summary>
public interface ICurrentUser
{
    ActorKind Kind { get; }

    string? Name { get; }

    string GetUserId();

    Guid? GetEmployeeId();

    string? GetUserEmail();

    bool IsAuthenticated();
}
