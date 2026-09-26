using NodaTime.Text;
using NodaTime.TimeZones;
using Wayd.Common.Application.TimeZones.Dtos;

namespace Wayd.Common.Application.TimeZones.Queries;

/// <summary>
/// The IANA time zones a user can choose from: the tz database's geographic zones plus UTC, by id.
/// </summary>
/// <remarks>
/// Served rather than taken from the browser so a picker offers exactly the ids the server accepts; the
/// browser's list follows its own tz database version.
/// </remarks>
public sealed record GetTimeZonesQuery : IQuery<List<TimeZoneDto>>;

public sealed class GetTimeZonesQueryHandler(IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTimeZonesQuery, List<TimeZoneDto>>
{
    private static readonly OffsetPattern _offsetFormat = OffsetPattern.CreateWithInvariantCulture("+HH:mm");

    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public Task<List<TimeZoneDto>> Handle(GetTimeZonesQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var source = TzdbDateTimeZoneSource.Default;

        // Zone locations are the tz database's zone.tab: zones tied to a country, which leaves out UTC. UTC is
        // the settings default and a real Tzdb id, so without it the picker could not show or restore it.
        var ids = (source.ZoneLocations ?? [])
            .Select(l => l.ZoneId)
            .Append("UTC")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        var zones = ids
            .Select(id => new TimeZoneDto
            {
                Id = id,
                CurrentOffset = _offsetFormat.Format(DateTimeZoneProviders.Tzdb[id].GetUtcOffset(now)),
            })
            .ToList();

        return Task.FromResult(zones);
    }
}
