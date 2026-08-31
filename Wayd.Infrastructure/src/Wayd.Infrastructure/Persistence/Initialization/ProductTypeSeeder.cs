using Microsoft.EntityFrameworkCore;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default product types an organization starts from.
/// </summary>
/// <remarks>
/// <para>
/// Initial seed only: once any type exists, seeded or admin-created, this stays out of the way. This
/// differs from the workflow seeder, which seeds per owner type so a newly added owner type still
/// gets its default.
/// </para>
/// <para>
/// <strong>Adding a system type later takes two steps.</strong> Add it here so new installs get it,
/// <em>and</em> write a data migration to insert it into existing ones — this seeder will not, because
/// it returns early the moment any type exists. Skipping the migration strands every existing
/// organization without the new type permanently: <c>Name</c> is uniquely indexed, so once a tenant
/// hand-creates one by that name the platform can never introduce its own. Follow the shape of
/// <c>Seed-External-Identity-Mappings</c>.
/// </para>
/// <para>
/// Seeded as system types because they are read-only to admins — <c>DeleteProductTypeCommand</c>
/// refuses them and only deactivation is offered, so an organization that stops shipping tools keeps
/// "Tool" switched off rather than removing it. An organization wanting different releasability or
/// naming creates its own alongside these.
/// </para>
/// </remarks>
public class ProductTypeSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        // Guarded on its own count rather than sharing the types guard below: an install holding types
        // but no axes would otherwise never get the Platform axis, and there is no recovery — the axis
        // is IsSystem, and the create command only makes non-system ones.
        var seededAxes = await SeedTagCategories(dbContext, cancellationToken);

        if (await dbContext.ProductTypes.AnyAsync(cancellationToken))
        {
            if (seededAxes)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // Ordered commercial-first, matching how the taxonomy reads top-down: groupings, then the
        // products themselves, then the assets that implement them.
        //
        // IsReleasable is the flag that stops a unified tree degrading into area paths. A release cut
        // against an abstract grouping is nonsense the model should refuse; an embedded node — a
        // connector compiled into the API — has no version of its own and ships inside its host's
        // release. Phase two adds the remaining capability flags around these.
        (string Name, string Description, bool IsReleasable)[] types =
        [
            ("Product Line", "A logical grouping of products. Not released in its own right.", false),
            ("Platform", "A technical foundation other products are built on. Grouping only — releases are cut against the services beneath it.", false),
            ("Product", "A commercial offering, presented to customers as one thing.", true),
            ("Application", "A deployed user-facing application.", true),
            ("Service", "A deployed service or API implementation.", true),
            ("Tool", "Something published to a registry and installed by consumers you do not control.", true),
            ("Library", "Published with its own version and consumed as a dependency rather than run.", true),
            ("Module", "Code with distinct ownership or dependencies that ships inside another node.", false),
            ("Interface", "A surface a node exposes — OpenAPI, GraphQL, gRPC, MCP. Ships with its provider.", false),
        ];

        var order = 1;
        foreach (var (name, description, isReleasable) in types)
        {
            dbContext.ProductTypes.Add(ProductType.CreateSystem(name, description, isReleasable, order++));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the one tag axis worth assuming every organization wants.
    /// </summary>
    /// <remarks>
    /// Platform is where web-versus-mobile lives, deliberately as a label rather than a node type: a web
    /// app and a mobile app behave identically — both releasable, both deployed, both rolling up into a
    /// product — so splitting the type system on it would add types that gate nothing.
    /// <para>
    /// Only this axis is seeded. Tech stack, compliance scope and team conventions are all real axes, but
    /// which ones an organization needs is exactly what cannot be guessed, and an unwanted seeded axis is
    /// something every organization has to remove.
    /// </para>
    /// </remarks>
    /// <returns>Whether anything was added.</returns>
    private static async Task<bool> SeedTagCategories(WaydDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.ProductTagCategories.AnyAsync(cancellationToken))
        {
            return false;
        }

        // AllowsMany: a cross-platform app genuinely targets iOS and Android, and forcing one would
        // record something false.
        var platform = ProductTagCategory.CreateSystem(
            "Platform",
            "What a node runs on or targets.",
            allowsMany: true,
            order: 1);

        foreach (var (name, description) in new[]
        {
            ("web", "Runs in a browser."),
            ("ios", "Apple mobile."),
            ("android", "Android mobile."),
            ("desktop", "Installed on a desktop operating system."),
            ("cli", "Run from a terminal."),
            ("server", "Runs on infrastructure rather than a user's device."),
        })
        {
            platform.AddSystemTag(name, description);
        }

        dbContext.ProductTagCategories.Add(platform);

        return true;
    }
}
