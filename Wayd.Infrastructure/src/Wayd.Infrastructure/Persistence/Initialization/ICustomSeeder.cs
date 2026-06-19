namespace Wayd.Infrastructure.Persistence.Initialization;

public interface ICustomSeeder
{
    Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken);
}