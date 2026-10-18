# Product dependencies

A product can record which other products it relies on. Each dependency has a **strength**, optional
**interaction styles**, an optional description, and the days it held (`startsOn`, and `endsOn` once
it stopped).

## Reading them rolls up the tree

`Products_GetDependencies` reports `dependsOn` and `usedBy` **across everything beneath the
product**: a product line's `dependsOn` includes its services' links to outside products, and a link
with both ends inside the line appears in neither list. `productPath` and `dependsOnProductPath` say
which descendant each row starts or lands on.

An empty answer means nothing has been **recorded** — dependencies are entered by hand or imported,
never discovered — so say that rather than "nothing depends on it".

## Recording them

Record each with `Products_AddDependency`.

- **Record the most specific product known** — the service that makes the call, not its platform.
  The read side rolls it up to every ancestor anyway, and a link on the platform cannot be rolled
  down.
- **Strength has no default.** `Hard`: the product stops working without it. `Soft`: it degrades or
  loses a feature but keeps working. Impact attribution reads this, so ask the person if they have not
  said rather than guessing.
- **Interaction styles refine strength.** A Hard **synchronous** dependency caps the consumer's
  availability at the provider's; a Hard **asynchronous** one turns the provider's downtime into a
  backlog worked through afterwards. Omitting them records nothing rather than recording that there
  are none, so `null` is never evidence either way.

## Changing them keeps history

| The situation | Tool |
| --- | --- |
| Strength or styles changed, or styles are being written down for the first time | `Products_ChangeDependencyTerms` — the default for any terms change. A real change ends the dependency and opens a new one; first-time styles are filled in place |
| Styles never recorded, on an ended dependency | `Products_UpdateDependency` (`Products_ChangeDependencyTerms` refuses ended dependencies) |
| The description is wrong | `Products_UpdateDependency` |
| It was true and has stopped | `Products_EndDependency` — kept, and still counts for the days it held |
| It was recorded on the wrong terms from its first day | `Products_RemoveDependency`, then `Products_AddDependency` — terms can only change from the day after it started |
| It was never true | `Products_RemoveDependency` — deletes it, with a reason |

After `Products_ChangeDependencyTerms`, use the id it returns for any further call: when the terms
changed, the id you passed now belongs to the ended dependency.

Every tool that changes a dependency takes, as `id`, the product that *has* it — `product.id` on the
row, which on a `usedBy` row or a rolled-up row is not the product you asked about.

For many at once, the `product-management.product-dependencies` import (see **wayd-imports**) applies
**product by product** — every row for one `ProductId` saves together or not at all, and the other
products still import — so read a preflight's rejected rows by product.
