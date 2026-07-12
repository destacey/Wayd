namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The kind of company being generated, which sets what share of employees sit inside the product-delivery
/// team structure versus the rest of the business (sales, support, HR, …).
/// </summary>
public enum CompanyType
{
    /// <summary>A product/tech firm: most employees are part of the delivery team structure (~85%).</summary>
    Tech,

    /// <summary>A balanced business: roughly half the employees are in the delivery structure (~50%).</summary>
    Balanced,

    /// <summary>A non-delivery-led enterprise: only a minority are in the delivery structure (~20%).</summary>
    Enterprise,
}

/// <summary>Knobs for the generated organization. Sensible defaults produce a small, realistic tech company.</summary>
public sealed class OrgOptions
{
    /// <summary>Number of leaf delivery teams. Distributed across ARTs, which are distributed across value streams.</summary>
    public int Teams { get; init; } = 18;

    /// <summary>
    /// Number of value streams (product lines). Each is a top-level grouping. Larger value streams become a
    /// three-tier Value Stream → ART → Team hierarchy; smaller ones collapse to a two-tier ART → Team.
    /// </summary>
    public int ValueStreams { get; init; } = 3;

    /// <summary>The kind of company, which sets the default delivery ratio (see <see cref="DeliveryRatio"/>).</summary>
    public CompanyType CompanyType { get; init; } = CompanyType.Tech;

    /// <summary>
    /// Optional override for the share (0..1) of employees who sit inside the delivery team structure. When
    /// null, the value is derived from <see cref="CompanyType"/>. The delivery headcount comes from the team
    /// hierarchy; the non-delivery (outside) headcount is sized to hit this ratio.
    /// </summary>
    public double? DeliveryRatio { get; init; }

    /// <summary>Fraction (0..1) of non-delivery employees generated as former (inactive) employees.</summary>
    public double FormerEmployeeFraction { get; init; } = 0.08;

    /// <summary>Optional fixed seed for reproducible output. Null = nondeterministic.</summary>
    public int? Seed { get; init; }

    /// <summary>The effective share of employees inside the delivery structure — the override, or the type default.</summary>
    public double EffectiveDeliveryRatio => DeliveryRatio ?? CompanyType switch
    {
        CompanyType.Tech => 0.85,
        CompanyType.Balanced => 0.5,
        CompanyType.Enterprise => 0.2,
        _ => 0.85,
    };
}
