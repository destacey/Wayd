using Wayd.Common.Application.SystemSettings.Scheduling.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Queries;

public sealed record GetSchedulingSettingsQuery : IQuery<SchedulingSettingsDto>;

public sealed class GetSchedulingSettingsQueryHandler(ISettings<SchedulingSettings> settings)
    : IQueryHandler<GetSchedulingSettingsQuery, SchedulingSettingsDto>
{
    private readonly ISettings<SchedulingSettings> _settings = settings;

    public async Task<SchedulingSettingsDto> Handle(GetSchedulingSettingsQuery request, CancellationToken cancellationToken)
    {
        var values = await _settings.Get(cancellationToken);

        return new SchedulingSettingsDto
        {
            DefaultTimeZone = values.DefaultTimeZone,
            DefaultCommitmentGraceDays = values.DefaultCommitmentGraceDays,
        };
    }
}
