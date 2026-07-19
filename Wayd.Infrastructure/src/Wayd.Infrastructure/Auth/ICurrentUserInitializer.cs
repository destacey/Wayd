namespace Wayd.Infrastructure.Auth;

public interface ICurrentUserInitializer
{
    void SetCurrentUserId(string userId);
}
