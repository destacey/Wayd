using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default status workflows for Product Management's five owner types.
/// </summary>
/// <remarks>
/// Deliberately minimal: each workflow carries the aliases its aggregates cannot work without, plus a
/// starting status, and nothing else. After adding a status every record still maps on its own, but a
/// dropped status that records hold maps to nothing, and someone has to decide where those records go.
/// Under-seeding is the cheaper mistake. <see cref="SystemWorkflowSeeding"/> holds the rules every module's
/// defaults are seeded by.
/// </remarks>
public class ProductManagementWorkflowSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        ProductWorkflowOwners.Register();

        var seeding = new SystemWorkflowSeeding();
        var seeded = false;

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Product, "Default Product Workflow",
            "The lifecycle of a product node.",
            [
                ("Concept", "Proposed but not yet in use.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Active", "Live and in use.", StatusCategory.Active, ProductStatusAlias.Active),
                ("Sunset", "No longer offered, still supported.", StatusCategory.Active, ProductStatusAlias.Sunset),
                ("Retired", "Withdrawn from service.", StatusCategory.Done, ProductStatusAlias.Retired),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Version, "Default Version Workflow",
            "The lifecycle of a versioned cut of one product.",
            [
                ("Planned", "Scheduled but not yet cut.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Cut and ready to ship.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Shipped.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Pulled after being cut.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        // Shares the version vocabulary but not its meaning: an announcement is drafted and announced
        // where a version is cut and shipped. Ready is the resting state before the announcement goes
        // out, not evidence that anything was cut.
        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Release, "Default Release Workflow",
            "The lifecycle of a release as announced to customers.",
            [
                ("Planned", "Drafted but not yet announced.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Ready to announce.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Announced to customers.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Retracted after being announced.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.ReleasePackage, "Default Release Package Workflow",
            "The lifecycle of a coordinated shipment of several component releases.",
            [
                ("Planned", "Assembled but not yet ready.", StatusCategory.Proposed, ProductStatusAlias.None),
                ("Ready", "Ready to ship.", StatusCategory.Active, ProductStatusAlias.Ready),
                ("Released", "Shipped.", StatusCategory.Done, ProductStatusAlias.Released),
                ("Withdrawn", "Pulled after being assembled.", StatusCategory.Removed, ProductStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        seeded |= await seeding.SeedIfAbsent(dbContext, ProductWorkflowOwners.Deployment, "Default Deployment Workflow",
            "The outcome of one version or package reaching one environment.",
            [
                ("In Progress", "Under way, with no outcome yet.", StatusCategory.Active, ProductStatusAlias.InProgress),
                ("Succeeded", "Reached its environment.", StatusCategory.Done, ProductStatusAlias.Succeeded),
                ("Failed", "Did not reach its environment.", StatusCategory.Removed, ProductStatusAlias.Failed),
                ("Rolled Back", "Reached its environment and was reverted.", StatusCategory.Removed, ProductStatusAlias.RolledBack),
            ], dateTimeProvider, cancellationToken);

        if (seeded)
        {
            await seeding.Save(dbContext, cancellationToken);
        }
    }
}
