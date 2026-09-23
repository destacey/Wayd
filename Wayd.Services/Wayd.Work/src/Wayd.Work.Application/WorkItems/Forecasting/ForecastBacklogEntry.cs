namespace Wayd.Work.Application.WorkItems.Forecasting;

internal sealed record ForecastBacklogEntry(Guid Id, WorkItemKey Key, string Title);
