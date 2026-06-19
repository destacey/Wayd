using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement;

public interface IFeatureManagementDbContext : IWaydDbContext
{
    DbSet<FeatureFlag> FeatureFlags { get; }
}
