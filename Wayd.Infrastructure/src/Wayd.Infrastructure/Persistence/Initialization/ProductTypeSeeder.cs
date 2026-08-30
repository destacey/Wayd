using Microsoft.EntityFrameworkCore;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default product types an organization starts from.
/// </summary>
/// <remarks>
/// <para>
/// Initial seed only: once any type exists, seeded or admin-created, this stays out of the way. The
/// types are a set an organization curates — deleting "Tool" because it does not ship one is a
/// legitimate choice, and recreating it on the next startup would undo that. This differs from the
/// workflow seeder, which seeds per owner type so a newly added owner type still gets its default.
/// </para>
/// <para>
/// Seeded as system types so an upgrade can reseed them safely; an organization that wants different
/// releasability or naming creates its own rather than editing these.
/// </para>
/// </remarks>
public class ProductTypeSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.ProductTypes.AnyAsync(cancellationToken))
        {
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
            ("Product", "A commercial offering, presented to customers as one thing.", true),
            ("Application", "A deployed user-facing application.", true),
            ("Service", "A deployed service or API implementation.", true),
            ("Tool", "Something published to a registry and installed by consumers you do not control.", true),
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
}
