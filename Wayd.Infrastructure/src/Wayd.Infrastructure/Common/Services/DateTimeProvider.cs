using NodaTime;

namespace Wayd.Infrastructure.Common.Services;

public class DateTimeProvider(TimeProvider timeProvider) : IDateTimeProvider
{
    public Instant Now => Instant.FromDateTimeOffset(timeProvider.GetUtcNow());

    public LocalDate Today => Now.InUtc().Date;
}
