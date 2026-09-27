using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// How a team works for a span of days. A team's models never overlap and the latest is open-ended.
/// </summary>
/// <remarks>
/// A change from a date opens a new model, so history before it keeps the old values; editing a model in
/// place corrects its whole period.
/// </remarks>
public abstract class OperatingModel : BaseAuditableEntity
{
    protected OperatingModel() { }

    protected OperatingModel(OperatingModelDateRange dateRange, string timeZone)
    {
        DateRange = dateRange;
        TimeZone = timeZone;
    }

    /// <summary>Gets the effective date range for this operating model.</summary>
    public OperatingModelDateRange DateRange { get; protected set; } = null!;

    /// <summary>Gets the IANA id of the time zone the team's days are counted in.</summary>
    public string TimeZone { get; protected set; } = null!;

    /// <summary>Gets whether this operating model is current (has no end date).</summary>
    public bool IsCurrent => DateRange.IsCurrent;

    /// <summary>
    /// Closes this operating model by setting its end date.
    /// </summary>
    /// <param name="endDate">The end date for this operating model.</param>
    internal void Close(LocalDate endDate)
    {
        DateRange.SetEnd(endDate);
    }

    /// <summary>
    /// Clears the end date from the current date range, leaving only the start date set.
    /// </summary>
    internal void ClearEndDate()
    {
        DateRange.ClearEnd();
    }

    protected static Result ValidateTimeZone(string timeZone)
    {
        // Stored zones are resolved against NodaTime's bundled Tzdb, so accept only ids it knows.
        return string.IsNullOrWhiteSpace(timeZone) || DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone) is null
            ? Result.Failure($"'{timeZone}' is not a valid IANA time zone.")
            : Result.Success();
    }

    /// <summary>
    /// Opens the date range of a model starting on <paramref name="startDate"/>, closing
    /// <paramref name="currentModel"/> the day before. Call only once the new model's values are valid, since
    /// the current model is closed on success.
    /// </summary>
    protected static Result<OperatingModelDateRange> OpenFrom(LocalDate startDate, OperatingModel? currentModel)
    {
        if (currentModel is not null && currentModel.IsCurrent)
        {
            if (startDate <= currentModel.DateRange.Start)
                return Result.Failure<OperatingModelDateRange>("New operating model start date must be after the current model's start date.");

            currentModel.Close(startDate.PlusDays(-1));
        }

        return new OperatingModelDateRange(startDate, null);
    }

    /// <summary>
    /// Removes the current model from <paramref name="models"/> and reopens the one before it.
    /// </summary>
    /// <returns>The removed model and the reinstated one.</returns>
    internal static Result<(TModel Removed, TModel Reinstated)> RemoveCurrent<TModel>(List<TModel> models, Guid operatingModelId)
        where TModel : OperatingModel
    {
        var operatingModel = models.SingleOrDefault(m => m.Id == operatingModelId);
        if (operatingModel is null)
            return Result.Failure<(TModel, TModel)>($"Operating model with Id {operatingModelId} not found.");

        if (!operatingModel.IsCurrent)
            return Result.Failure<(TModel, TModel)>("Only the current operating model can be removed. Historical operating models must be preserved to maintain data integrity.");

        if (models.Count == 1)
            return Result.Failure<(TModel, TModel)>("Cannot remove the last operating model. At least one operating model must remain.");

        var previousModel = models
            .Where(m => m.Id != operatingModelId)
            .OrderByDescending(m => m.DateRange.Start)
            .First();

        previousModel.ClearEndDate();
        models.Remove(operatingModel);

        return (operatingModel, previousModel);
    }
}
