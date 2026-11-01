using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the Request Desk areas are known by, so dependencies read as references.</summary>
public static class DemandManagementArea
{
    public const string FeatureFlag = "demand-management.feature-flag";
    public const string Settings = "demand-management.settings";
    public const string Desks = "demand-management.desks";
    public const string IntakeRequests = "demand-management.intake-requests";

    /// <summary>The key the settings area publishes tag ids under, each keyed <c>axis|tag</c>.</summary>
    public const string Tags = "demand-management.tags";

    /// <summary>The key the settings area publishes channel ids under, keyed by name.</summary>
    public const string Channels = "demand-management.channels";

    /// <summary>The feature flag every Demand Management endpoint is gated on.</summary>
    public const string FeatureFlagName = "demand-management";
}

/// <summary>A Request Desk area, which has nothing to do when the seed was asked to skip it.</summary>
public abstract class DemandManagementSeedArea(string name, params string[] dependsOn) : SeedArea(name, dependsOn)
{
    protected GeneratedDemandManagement Data(SeedContext context) =>
        context.DemandManagement ?? throw new SeedException($"Area '{Name}' ran without a generated Request Desk dataset.");
}

/// <summary>
/// Switches the module on. Every Demand Management endpoint answers 404 while its flag is off, and the flag is
/// seeded off.
/// </summary>
public sealed class DemandManagementFeatureFlagArea() : DemandManagementSeedArea(DemandManagementArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.DemandManagement is not null;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var enabled = await context.Client.EnsureFeatureFlagEnabled(DemandManagementArea.FeatureFlagName, cancellationToken);

        context.Log(enabled
            ? $"Enabled the '{DemandManagementArea.FeatureFlagName}' feature flag."
            : $"The '{DemandManagementArea.FeatureFlagName}' feature flag is already enabled.");
    }
}

/// <summary>
/// Creates the tag axes and their tags, and reads the channels Wayd seeded, through the ordinary endpoints:
/// they are a company's settings, not records anyone imports.
/// </summary>
public sealed class DemandManagementSettingsArea() : DemandManagementSeedArea(
    DemandManagementArea.Settings, DemandManagementArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.DemandManagement is not null;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var categories = Data(context).TagCategories;

        context.Log($"Ensuring {categories.Count} intake request tag axes exist...");
        context.Publish(DemandManagementArea.Tags, await context.Client.EnsureIntakeTagCategories(categories, cancellationToken));

        context.Publish(DemandManagementArea.Channels, await context.Client.GetIntakeChannelIds(cancellationToken));
        context.Publish(Name, new Dictionary<string, Guid>());
    }
}

/// <summary>
/// Creates the desks with the people who work them, through the ordinary endpoint. Members are employees, so
/// the employee import goes first.
/// </summary>
public sealed class IntakeDesksArea() : DemandManagementSeedArea(
    DemandManagementArea.Desks, DemandManagementArea.FeatureFlag, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.DemandManagement?.Desks.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var desks = Data(context).Desks;
        context.Log($"Ensuring {desks.Count} intake desks exist...");

        var created = await context.Client.EnsureIntakeDesks(desks, context.Ids(OrganizationArea.Employees), cancellationToken);
        context.Publish(Name, created);
    }
}

/// <summary>
/// Loads the requests, resolving each one's desk, channel, tags and people. A channel the environment no
/// longer has is left off rather than failing the row: it records where the ask came from, and is optional.
/// </summary>
public sealed class IntakeRequestsArea() : DemandManagementSeedArea(
    DemandManagementArea.IntakeRequests, DemandManagementArea.Settings, DemandManagementArea.Desks, OrganizationArea.Employees)
{
    public override string BatchedImport => "demand-management.intake-requests";

    public override bool ShouldRun(SeedContext context) => context.DemandManagement?.Requests.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var requests = Data(context).Requests;
        var channels = context.Ids(DemandManagementArea.Channels);

        var rows = requests.Select(r => new IntakeRequestCsvRow
        {
            ImportId = r.Handle,
            Title = r.Title,
            Description = r.Description,
            ExpectedBenefits = r.ExpectedBenefits,
            DeskId = context.Id(DemandManagementArea.Desks, r.DeskName),
            ChannelId = channels.TryGetValue(r.ChannelName, out var channelId) ? channelId : null,
            RequesterId = context.Id(OrganizationArea.Employees, r.RequesterEmployeeNumber),
            SubmittedAt = DeploymentCsvRow.Timestamp(r.SubmittedAt),
            NeedByDate = r.NeedByDate,
            NeedByBasis = r.NeedByBasis,
            NeedByReference = r.NeedByReference,
            NeedByCommittedById = r.NeedByCommittedByEmployeeNumber is { } committedBy
                ? context.Id(OrganizationArea.Employees, committedBy)
                : null,
            NeedByCommittedOn = r.NeedByCommittedOn,
            OriginSourceSystem = r.OriginSourceSystem,
            OriginExternalId = r.OriginExternalId,
            OriginUrl = r.OriginUrl,
            Status = r.Status,
            StatusChangedAt = r.StatusChangedAt is { } changed ? DeploymentCsvRow.Timestamp(changed) : null,
            StatusReason = r.StatusReason,
            AssigneeId = r.AssigneeEmployeeNumber is { } assignee ? context.Id(OrganizationArea.Employees, assignee) : null,
            TagIds = r.Tags.Count == 0 ? null : string.Join(';', context.Ids(DemandManagementArea.Tags, r.Tags)),
        }).ToList();

        var batches = Batch(context, rows, r => r.ImportId);
        context.Log($"Importing {rows.Count} intake requests in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "intake requests", batches,
            batch => context.Client.ImportIntakeRequests(CsvFile.ToBytes(batch), cancellationToken));

        context.Publish(Name, created);
    }
}
