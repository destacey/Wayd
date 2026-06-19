using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Tests.Data.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class TestHealthCheckFaker : PrivateConstructorFaker<TestHealthCheck>
{
    public TestHealthCheckFaker(Instant reportedOn, Duration? expirationOffset = null)
    {
        var offset = expirationOffset ?? Duration.FromDays(14);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Status, f => f.PickRandom<HealthStatus>());
        RuleFor(x => x.ReportedById, f => f.Random.Guid());
        RuleFor(x => x.ReportedOn, reportedOn);
        RuleFor(x => x.Expiration, reportedOn.Plus(offset));
        RuleFor(x => x.Note, f => f.Lorem.Sentence(8));
    }
}
