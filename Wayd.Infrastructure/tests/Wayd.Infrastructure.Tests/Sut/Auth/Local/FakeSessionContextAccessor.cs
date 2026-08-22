using Wayd.Infrastructure.Auth.Local;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Local;

/// <summary>
/// Supplies a fixed device context to <see cref="TokenService"/> tests. Set
/// <see cref="Current"/> to vary what a sign-in records.
/// </summary>
internal sealed class FakeSessionContextAccessor : ISessionContextAccessor
{
    public SessionContext Current { get; set; } = new("test-agent", "198.51.100.7");
}
