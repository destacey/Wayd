using System.Text;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The time and randomness every generator draws from, so one run produces one coherent dataset.
/// </summary>
/// <remarks>
/// Both halves exist because a run used to have neither. Dates came from "now" independently in three
/// places — Bogus's own reference date, two hardcoded age constants, and a static <c>Today</c> — so the
/// same seed produced different data tomorrow while the tool printed "pass --random-seed N to reproduce
/// this data". Seeds were derived as <c>seed + 1</c> per generator, which means inserting a generator
/// shifts every later one's data.
/// <para>
/// <see cref="AsOf"/> is the run's "now" and <see cref="FoundedOn"/> is a floor, not an origin: nothing
/// may predate the founding and no record may predate the one it hangs off, but the layers still span
/// their own spreads. Collapsing every date onto one start reads as synthetic.
/// </para>
/// </remarks>
public sealed class GenerationContext
{
    /// <summary>
    /// The date the run treats as today. Defaults to the real one, so generated data straddles now; pin it
    /// together with <see cref="Seed"/> for byte-identical output, since either alone is insufficient.
    /// </summary>
    public required DateTime AsOf { get; init; }

    /// <summary>The root seed. Generators take a derived one from <see cref="SeedFor"/> rather than this.</summary>
    public required int Seed { get; init; }

    /// <summary>How far back the company's history reaches. Employees are hired across this whole span.</summary>
    public int CompanyAgeYears { get; init; } = 5;

    /// <summary>How long the current team structure has stood, which is shorter than the company's own age.</summary>
    public int TeamStructureAgeYears { get; init; } = 2;

    /// <summary>How much delivery history sits behind <see cref="AsOf"/>.</summary>
    public int HistoryYears { get; init; } = 2;

    /// <summary>How much planned work sits ahead of <see cref="AsOf"/>.</summary>
    public int RunwayYears { get; init; } = 2;

    /// <summary>The floor: no generated date may fall before the company existed.</summary>
    public DateTime FoundedOn => AsOf.AddYears(-CompanyAgeYears);

    /// <summary>The start of the delivery window that work is placed on.</summary>
    public DateTime WindowStart => AsOf.AddYears(-HistoryYears);

    /// <summary>The end of the delivery window that work is placed on.</summary>
    public DateTime WindowEnd => AsOf.AddYears(RunwayYears);

    /// <summary>
    /// The seed one named area generates from, derived from the root seed and the area's name.
    /// </summary>
    /// <remarks>
    /// Derived by name rather than by position so that adding an area leaves every other area's data
    /// untouched. Offsetting a shared seed per generator does the opposite: inserting one shifts all of
    /// them, which breaks quietly once anyone pins a seed and expects the same data back.
    /// </remarks>
    public int SeedFor(string area) => unchecked((int)Fnv1a(area, (uint)Seed));

    /// <summary>
    /// FNV-1a over the area name, folded into the seed.
    /// </summary>
    /// <remarks>
    /// Hand-rolled because <see cref="string.GetHashCode()"/> is randomized per process: it would derive
    /// a different area seed on every run, so a pinned seed would reproduce nothing. Anything used here
    /// has to be stable across processes and across framework versions.
    /// </remarks>
    private static uint Fnv1a(string value, uint basis)
    {
        const uint prime = 16777619;

        var hash = basis ^ 2166136261;
        foreach (var b in Encoding.UTF8.GetBytes(value))
            hash = unchecked((hash ^ b) * prime);

        return hash;
    }
}
