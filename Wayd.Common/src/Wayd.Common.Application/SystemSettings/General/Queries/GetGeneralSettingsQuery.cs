using Wayd.Common.Application.SystemSettings.General.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General.Queries;

/// <summary>Reads the general settings in effect, with the defaults for anything never saved.</summary>
public sealed record GetGeneralSettingsQuery : IQuery<GeneralSettingsDto>;

/// <summary>Handles <see cref="GetGeneralSettingsQuery"/>.</summary>
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
