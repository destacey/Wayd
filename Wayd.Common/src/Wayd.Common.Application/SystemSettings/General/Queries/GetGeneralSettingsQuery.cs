using Wayd.Common.Application.SystemSettings.General.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General.Queries;

public sealed record GetGeneralSettingsQuery : IQuery<GeneralSettingsDto>;

public sealed class GetGeneralSettingsQueryHandler(ISettings<GeneralSettings> settings)
    : IQueryHandler<GetGeneralSettingsQuery, GeneralSettingsDto>
{
    private readonly ISettings<GeneralSettings> _settings = settings;

    public async Task<GeneralSettingsDto> Handle(GetGeneralSettingsQuery request, CancellationToken cancellationToken)
    {
        var values = await _settings.Get(cancellationToken);

        return new GeneralSettingsDto { TimeZone = values.TimeZone };
    }
}
