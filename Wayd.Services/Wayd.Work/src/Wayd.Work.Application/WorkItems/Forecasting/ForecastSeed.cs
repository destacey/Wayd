namespace Wayd.Work.Application.WorkItems.Forecasting;

internal static class ForecastSeed
{
    /// <summary>
    /// A seed for what is being forecast on a given day, so the same inputs on the same day give
    /// the same forecast on every request while tomorrow's is drawn afresh.
    /// </summary>
    public static int From(Guid subject, LocalDate start)
    {
        Span<byte> bytes = stackalloc byte[16];
        subject.TryWriteBytes(bytes);

        return BitConverter.ToInt32(bytes[..4])
            ^ BitConverter.ToInt32(bytes[4..8])
            ^ BitConverter.ToInt32(bytes[8..12])
            ^ BitConverter.ToInt32(bytes[12..])
            ^ start.ToDateOnly().DayNumber;
    }
}
