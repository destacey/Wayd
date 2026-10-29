using Wayd.Common.Application.SystemSettings.ProductTags.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.ProductTags.Queries;

public sealed record GetProductTagSettingsQuery : IQuery<ProductTagSettingsDto>;

public sealed class GetProductTagSettingsQueryHandler(ISettings<ProductTagSettings> settings)
    : IQueryHandler<GetProductTagSettingsQuery, ProductTagSettingsDto>
{
    private readonly ISettings<ProductTagSettings> _settings = settings;

    public async Task<ProductTagSettingsDto> Handle(GetProductTagSettingsQuery request, CancellationToken cancellationToken)
    {
        var values = await _settings.Get(cancellationToken);

        return new ProductTagSettingsDto { AllowsFreeTags = values.AllowsFreeTags };
    }
}
