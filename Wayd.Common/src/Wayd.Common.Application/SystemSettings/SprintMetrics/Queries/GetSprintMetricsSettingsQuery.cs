using Wayd.Common.Application.SystemSettings.SprintMetrics.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.SprintMetrics.Queries;

public sealed record GetSprintMetricsSettingsQuery : IQuery<SprintMetricsSettingsDto>;

public sealed class GetSprintMetricsSettingsQueryHandler(ISettings<SprintMetricsSettings> settings)
    : IQueryHandler<GetSprintMetricsSettingsQuery, SprintMetricsSettingsDto>
{
    private readonly ISettings<SprintMetricsSettings> _settings = settings;

    public async Task<SprintMetricsSettingsDto> Handle(GetSprintMetricsSettingsQuery request, CancellationToken cancellationToken)
    {
        var values = await _settings.Get(cancellationToken);

        return new SprintMetricsSettingsDto { SprintWindow = values.SprintWindow };
    }
}
