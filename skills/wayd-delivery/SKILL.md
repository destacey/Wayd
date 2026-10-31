---
name: wayd-delivery
description: Read before calling the Wayd MCP tools named Releases_*, Versions_*, ReleasePackages_* or Deployments_*. Guides agents working with Wayd product delivery via the Wayd MCP server — releases announced to customers, versions of individual artifacts, release packages that ship several components together, and deployments into environments. Use when recording or looking up what was announced, what was built, what shipped together, or where something was deployed; when setting what a release contains; when cutting, releasing, withdrawing or reverting a version or release, or correcting one entered wrongly; when assembling or amending a package manifest; when starting a deployment and recording whether it succeeded, failed or was rolled back; or when answering what shipped in a release, where a version went, or what is cut but not yet shipped.
---

# Wayd Delivery (Releases / Versions / Packages / Deployments)

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

## The four records, and why they are separate

**This is the section to read first.** The word "release" means three different things in most
organizations, and Wayd keeps them apart. Almost every mistake an agent makes here is picking the
wrong one of these.

| Record | Example | Answers | Does **not** hold |
| --- | --- | --- | --- |
| **Product** | `Wayd API` | What exists, and where it sits in the catalog | Any knowledge of versions |
| **Version** | `Wayd API 4.12.0` | What was **built** — one artifact, cut at a moment | Where it went |
| **Release Package** | `WAYD-2026.09.1` | What **moved through environments together** | A product of its own |
| **Release** | `Wayd 2026.09` | What was **announced to customers** | A cut date — it is never cut |

It reads as one sentence with no word doing double duty:

> **Release** 2026.09 shipped **package** WAYD-2026.09.1, containing Wayd API **version** 4.12.0,
> **deployed** to Production.

**Which one am I looking at?** If a customer would recognise the name, it is a *release*. If it names
one artifact and a version number, it is a *version*. If it is what the pipeline pushed, it is a
*package*.

> **The most common error.** Being asked to "record the 4.12.0 release" and calling `Releases_Plan`.
> `4.12.0` names one artifact, so it is a **version** — use `Versions_Plan`. Conversely, "we announced
> 2026.09 last Tuesday" is a **release**, not a version, even though it has a version-looking label.

### Tool map

| Record | Read | Lifecycle |
| --- | --- | --- |
| Version | `Versions_GetVersions`, `Versions_GetVersion`, `Versions_GetStatusHistory`, `Versions_GetActivities` | `Versions_Plan`, `Versions_Update`, `Versions_Cut`, `Versions_MarkReleased`, `Versions_Withdraw`, `Versions_Revert`, `Versions_CorrectDates`, `Versions_MoveTargetDate`, `Versions_Delete` |
| Release package | `ReleasePackages_GetReleasePackages`, `ReleasePackages_GetReleasePackage`, `ReleasePackages_GetStatusHistory`, `ReleasePackages_GetActivities` | `ReleasePackages_Assemble`, `ReleasePackages_SetManifest`, `ReleasePackages_MarkReleased`, `ReleasePackages_Withdraw`, `ReleasePackages_CorrectDates`, `ReleasePackages_Delete` |
| Release | `Releases_GetReleases`, `Releases_GetRelease`, `Releases_GetStatusHistory`, `Releases_GetActivities` | `Releases_Plan`, `Releases_Update`, `Releases_SetContents`, `Releases_MarkReleased`, `Releases_Withdraw`, `Releases_Revert`, `Releases_CorrectDates`, `Releases_MoveTargetDate`, `Releases_Delete` |
| Deployment | `Deployments_GetDeployments`, `Deployments_GetDeployment`, `Deployments_GetStatusHistory`, `Deployments_GetActivities` | `Deployments_Start`, `Deployments_Succeed`, `Deployments_Fail`, `Deployments_RollBack`, `Deployments_Delete` |

Environments, rollout and delivery measures belong to the **wayd-products** skill.

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File | Read it when |
| --- | --- |
| [reference/release-contents.md](reference/release-contents.md) | Setting what a release or package contains, or announcing a release (`Releases_MarkReleased`) |
| [reference/lifecycle-and-dates.md](reference/lifecycle-and-dates.md) | Withdrawing, reverting, correcting dates on, or moving the target of a version, release or package, or sending any date or instant |
| [reference/deployments.md](reference/deployments.md) | Starting a deployment or recording its outcome, or finding where a version was deployed |

---

## Ending a record versus deleting it

Every record has a normal way to end that keeps its history. Delete only a record created by mistake,
or when the user explicitly wants that history gone — and say what goes with it before confirming.

| Record | Normal end | Delete tool | Delete refused while | Delete also removes |
| --- | --- | --- | --- | --- |
| Version | `Versions_Withdraw` | `Versions_Delete` | A release lists it, or a package manifest names it | Its status history and every deployment of it |
| Release | `Releases_Withdraw` | `Releases_Delete` | Never — any state | Its contents list and status history; the versions and packages it named are kept |
| Release package | `ReleasePackages_Withdraw` | `ReleasePackages_Delete` | Any release lists it | Its manifest, status history and every deployment of it; the versions it names are kept |
| Deployment | Its outcome — `Deployments_Fail` or `Deployments_RollBack` for one that went wrong | `Deployments_Delete` | Never — any state | Its status history; the measures stop counting it |
| Environment | Retired — `DeploymentEnvironments_SetActive` with `isActive: false` | `DeploymentEnvironments_Delete` | It has deployments and the caller cannot delete deployments | Every deployment into it — state its `deploymentCount` first |

An announced or withdrawn release's contents are frozen, so a package it lists can only be freed by
deleting that release. Never delete a deployment that really failed or was rolled back: it would hide
a change failure.

Every tool that changes or removes a record is annotated so your client asks before running it. Treat
that as a genuine checkpoint rather than a formality: announcing a release is a statement to
customers, and withdrawing one retracts a statement already made.

---

## Overwrite, not patch

These tools replace what they cover rather than adding to it. Sending only what you want to change
silently clears or removes everything else.

| Tool | Replaces | Omitted or empty |
| --- | --- | --- |
| `Releases_SetContents` | Both routes at once — packages **and** directly-carried versions | Removed; two empty lists clear the release |
| `ReleasePackages_SetManifest` | Every manifest line | Removed; an empty manifest is refused |
| `Releases_Update`, `Versions_Update` | Every descriptive field | Cleared |
| `Versions_CorrectDates`, `Releases_CorrectDates`, `ReleasePackages_CorrectDates` | Every date on the record | An omitted target date (and a version's cut moment) is cleared |

**Patch instead** when you are adding or removing a few entries: everything the
patch does not name stays.

- `Releases_PatchContents` — add a package with `[{ "op": "add", "path": "/packageIds/-", "value": "<packageId>" }]`
  (likewise `/versionIds/-`). To remove one, `replace` that list with the ids that stay.
- `ReleasePackages_PatchManifest` — add a line with `[{ "op": "add", "path": "/components/-", "value": { "productId": "…", "versionId": "…", "version": "4.10.0", "kind": "Changed" } }]`.
  To drop a line, read the package and `replace` `/components` with every line that stays.

**Otherwise, always read the record first** and send back the full intended result. For `Releases_SetContents`
that means calling `Releases_GetRelease`, taking the existing `versions` and `packages`, applying your
change, and sending both complete lists.

---

## Ordering and version numbers

Version numbers and release labels are **free text and never parsed**. `4.8.2` and `2026.04` are both
just labels; Wayd never sorts or compares them. Ordering comes from released moments, with an optional
`sequence` override for the case where chronology misleads — a backport shipping after the version
that superseded it.

Do not infer precedence from a version string, and do not sort results by it.

---

## Typical flows

### Recording what shipped, end to end

1. `Versions_Plan` — the artifact that was built, against a **releasable** product.
2. `Versions_Cut`, then `Versions_MarkReleased`. For history entered after the fact, pass the past
   moments as `cutAt` and `releasedAt` (an instant with its offset, never a bare date — see
   [lifecycle-and-dates.md](reference/lifecycle-and-dates.md)); cutting is optional, and `Versions_MarkReleased` works on a
   version never cut. Use `Versions_CorrectDates` only to fix a moment already recorded wrongly — it
   moves no status, and it clears any date you do not resend.
3. `ReleasePackages_Assemble` — where several components shipped together. Name the **version record**
   on each manifest line, not just the version string, or the release will not know the version is
   already inside a package.
4. `Releases_Plan` — the announcement. Usually under a product **line**, or no product at all.
5. `Releases_PatchContents` or `Releases_SetContents` with both lists complete — the packages and any directly-carried versions.
6. `Releases_MarkReleased` — through the announcing checklist in [reference/release-contents.md](reference/release-contents.md).
7. `Deployments_Start` then an outcome — for each unit that reached an environment.

### Answering "what did we announce in X?"

`Releases_GetReleases` to find it, then `Releases_GetRelease` for its full contents.

### Answering "where did this version go?"

- `Releases_GetReleases` with `containingVersionId` — which announcements carried it, by either route.
- `ReleasePackages_GetReleasePackages` with `containingVersionId` — which packages shipped it.
- `Deployments_GetDeployments` with `versionId` — deployments of the version *itself*, which will be
  empty if it only ever shipped inside a package.
- `DeploymentEnvironments_GetRollout` — what is running in each environment **now**, one entry per
  product, with packages already expanded into their components. Prefer it over piecing the answer
  together from deployments: it applies the rollback and failed-attempt rules for you (see the
  **wayd-products** skill).

### Answering "what shipped lately?"

`DeliveryOverview_GetRecentDeliveryEvents` — one entry per version or package at its latest status
change, newest first. For cadence over a window, `DeliveryOverview_GetDeliveryOverview`. Both are
covered in the **wayd-products** skill.

### Answering "what changed on this, and who changed it?"

The `*_GetStatusHistory` tools cover status moves only. `Releases_GetActivities`,
`Versions_GetActivities`, `ReleasePackages_GetActivities` and `Deployments_GetActivities` cover
everything recorded on the record — contents and manifest changes, date corrections and target moves
included. Each entry's `payload` is a JSON string carrying both the old and new value, so a moved
target date shows where it moved from. Check `hasNextPage` before concluding something never happened.

### Answering "what is cut but not yet shipped?"

`Versions_GetVersions` with `statusCategory: Active` — a cut version that production has not received.
Without the filter, planned versions that were never cut and withdrawn ones come back too. This is a
version question, not a release one.

---

## Scoping and permissions

Releases carry their own permission, separate from the rest of delivery: a product manager drafting
`2026.09` is a different person from whoever records that the pipeline ran. A caller may hold one
without the other, so a tool refusing on authorization does not mean the whole area is unavailable.

A **version** can only be cut against a product whose type is *releasable*. A **release** has no such
restriction and is usually announced under a product line, which is typically not releasable — that
gate asks whether an artifact can be cut, which is a different question.

A release may name **no product at all** when it spans product lines. Filtering releases by product
deliberately excludes those: belonging to no single product, listing one under a product would
misstate what that product announced.
