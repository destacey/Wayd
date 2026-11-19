using Wayd.Common.Application.Interfaces;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Tests.Shared;

public class TestingDateTimeProvider : IDateTimeProvider
{
    private readonly FakeClock _clock;

    public TestingDateTimeProvider(IClock clock)
    {
        _clock = (FakeClock)clock;
    }

    public TestingDateTimeProvider(DateTime dateTime)
    {
        if (dateTime.Kind is DateTimeKind.Unspecified or DateTimeKind.Local)
        {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
        _clock = new FakeClock(dateTime.ToInstant());
    }

    public Instant Now => _clock.GetCurrentInstant();

    public void Advance(Duration duration)
    {
        _clock.Advance(duration);
    }
}
