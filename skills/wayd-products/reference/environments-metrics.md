# Environments and metrics

An **environment** is a named deployment target, defined once for the organization. Each carries a
**category** — Development, Testing, Staging, Production, or Other for a target that is none of those
(a sandbox, a demo box) — and a ring order for progressive rollout.

**The category is what every production-scoped measure counts on, not the name.** Pipeline
environment names are free text and endlessly varied (`prod`, `Production`, `prd`, `live`), so
filtering or reasoning by name will give wrong answers.

| The situation | Tool |
| --- | --- |
| A new deployment target | `DeploymentEnvironments_Create` — set the category deliberately |
| Rename it, reclassify it, or move its ring | `DeploymentEnvironments_Update` — resend every field; refused on a retired environment, so reinstate it first |
| It is no longer used | `DeploymentEnvironments_SetActive` with `isActive: false` — its deployments are kept and keep counting |
| It is back in use | `DeploymentEnvironments_SetActive` with `isActive: true` |
| The user explicitly wants it and its history gone | `DeploymentEnvironments_Delete` — takes **every deployment into it**; state its `deploymentCount` from `DeploymentEnvironments_GetDeploymentEnvironments` before confirming |

**Reclassifying changes the future, not the past.** Each deployment froze its environment's category
at the time, so promoting a staging environment to production does not retroactively inflate
deployment frequency.

## What is running where

`DeploymentEnvironments_GetRollout` lists every active environment in ring order with what is
**running** there: one entry per product, each the latest deployment that succeeded and was not
rolled back, with packages expanded into their components. A failed attempt leaves its predecessor
running, so check `hasFailedAttemptSince` before saying an environment is healthy.

## Two kinds of measure

`DeliveryMetrics_GetDeliveryMetrics` measures **deployments**; `DeliveryOverview_GetDeliveryOverview`
measures **versions** — what was cut and shipped, whether or not a pipeline recorded deployments. Use
the one that matches the question, and do not mix their figures.

From `DeliveryMetrics_GetDeliveryMetrics`, read the `unavailable` list rather than treating a missing
measure as zero, and report change failure rate as approximate: it is a pipeline proxy.

`DeliveryOverview_GetDeliveryOverview` scopes to a product **and everything beneath it** — a product
line is a valid scope. Pass the reader's IANA `timeZone`: it counts versions by the day they shipped,
and without it the days are UTC days, so a late-evening US release lands on the next day. Carry three
things into an answer:

- **Give the denominator.** `scope.releasableNodeCount` says how many products the figures cover.
- **A null `previousPerWeek` or `previousAverageDays` means nothing shipped in the prior window**, so
  there is no trend to report — not a rise from zero.
- **Cut-to-released excludes versions released without ever being cut** (imports and backfills).
  Report `measuredCount` of `releasedCount` alongside the average.

`DeliveryOverview_GetRecentDeliveryEvents` is the feed of what shipped lately. Reason on `alias`
(Ready, Released, Withdrawn), not on `statusName`, which is per-organization. Scoping it to a product
leaves packages out entirely.
