---
name: wayd-delivery
description: Guides agents working with Wayd product delivery via the Wayd MCP server — releases announced to customers, versions of individual artifacts, release packages that ship several components together, and deployments into environments. Use when recording or looking up what was announced, what was built, what shipped together, or where something was deployed; when setting what a release contains; when cutting, releasing, withdrawing or reverting a version or release; when assembling or amending a package manifest; or when starting a deployment and recording its outcome.
---

# Wayd Delivery (Releases / Versions / Packages / Deployments)

## When to use

- Finding what was announced to customers, and what a given announcement contained
- Recording a new release, version, package or deployment
- Setting or changing what a release announces
- Cutting a version, marking one released, or withdrawing one
- Announcing a release, retracting one, or correcting a record entered wrongly
- Assembling a package and its manifest, or replacing that manifest
- Starting a deployment and recording whether it succeeded, failed, or was rolled back
- Answering "what shipped in X?", "where did this version go?", or "what is cut but not yet shipped?"

---

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
| Environment | Retired — `DeploymentEnvironments_SetActive` with `isActive: false` | `DeploymentEnvironments_Delete` | Never | Every deployment into it — state its `deploymentCount` first |

An announced or withdrawn release's contents are frozen, so a package it lists can only be freed by
deleting that release. Never delete a deployment that really failed or was rolled back: it would hide
a change failure.

Every tool that changes or removes a record is annotated so your client asks before running it. Treat
that as a genuine checkpoint rather than a formality: announcing a release is a statement to
customers, and withdrawing one retracts a statement already made.

---

## Two rules that will refuse you

### A version is announced once, by one route

A release reaches its contents two ways, and may use both:

- **Packages** — the usual route, since a package is the deployment unit.
- **Versions carried directly** — for a single artifact that shipped alone, where nobody assembled a
  package.

**A version shipping inside one of the release's packages cannot also be carried directly.** Otherwise
one release announces the same shipment twice, and "what did 2026.09 contain" has two answers.

The rule is judged against what the release *ends up* containing — so moving a version out of the
direct list and into a package that ships it works, provided both changes go in the same
`Releases_SetContents` call. A manifest line that names no version record covers nothing and never
conflicts.

### A release cannot be announced while its contents have not shipped

`Releases_MarkReleased` is refused while any version or package the release carries has not shipped.
An **empty release announces normally** — a repackaging or a pricing change is announced with nothing
deployed, and emptiness is never the blocker.

To announce:

1. `Releases_GetRelease` — read the contents.
2. List every contents entry without a `releasedAt`.
3. For each, either release it (`Versions_MarkReleased` or `ReleasePackages_MarkReleased`, or a
   production deployment that succeeds) or remove it with `Releases_SetContents`.
4. Call `Releases_GetRelease` again. Repeat from step 2 until no entry lacks a `releasedAt`.
5. Confirm the announcement and its `releasedDate` with the user.
6. `Releases_MarkReleased`.

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

**Always read the record first** and send back the full intended result. For `Releases_SetContents`
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

## Lifecycle: withdraw versus revert

Both end an assertion, and choosing wrongly writes a history that misleads whoever reads it later.

| | Withdraw | Revert |
| --- | --- | --- |
| Tools | `Versions_Withdraw`, `Releases_Withdraw`, `ReleasePackages_Withdraw` | `Versions_Revert`, `Releases_Revert` — a package has no revert |
| What happened | It really shipped or was announced, then was pulled | It never shipped or was announced; the record was wrong |
| Resulting status | Terminal | Back to a live status |
| The released date or moment | Kept — it did happen | Cleared — it did not |
| Reason | Optional | **Required** |

Recording a mistake as a withdrawal leaves the append-only history asserting that somebody pulled
something nobody ever shipped, and a later reader has no way to tell.

**Correcting dates is a third thing.** `Versions_CorrectDates`, `Releases_CorrectDates` and
`ReleasePackages_CorrectDates` say a date was written down wrongly. The status does not move and the
status history is untouched. A package's released moment can only be corrected once it has been
released — the released moment is what closes the manifest, so it is set by
`ReleasePackages_MarkReleased`.

**Moving a target is a fourth.** `Versions_MoveTargetDate` and `Releases_MoveTargetDate` record that
the plan changed; omitting the date records that the record is no longer targeted. Both are refused
`Versions_MoveTargetDate` is refused on a released or withdrawn version, and
`Releases_MoveTargetDate` on a release in a terminal status.

**What happened is an instant; what is planned is a date.**

| Record | Instants (`...At`) | Calendar dates (`...Date`) |
| --- | --- | --- |
| Version | `cutAt` — the build or tag; `releasedAt` — when production received it | `targetDate` |
| Release package | `releasedAt` — when the pipeline run that shipped it completed | `targetDate` |
| Release | — | `targetDate`, `releasedDate` (the announcement is a business date) |
| Deployment | `startedAt`, `completedAt` | — |

Send an instant exactly as the CI/CD system reports it, with its offset (`2026-09-18T02:30:00Z`) — **never
convert it to a local date first**; a bare date is refused. A released moment of exactly 12:00 UTC may
be a converted calendar date; correct it with `Versions_CorrectDates` or `ReleasePackages_CorrectDates`
if the real moment matters.

A release's dates are calendar dates with no time and no offset. When deriving its released date from
a timestamp, convert it to the organization's local date: a late-evening US announcement lands after
midnight UTC and would otherwise be recorded a day late.

For time zones when reading delivery measures, see the **wayd-products** skill.

---

## Deployments

A deployment carries **either a version or a package, never both and never neither**. The request
schema cannot express that, so `Deployments_Start` is refused if you supply both or omit both.

**Where a package exists, deploy the package.** One pipeline run shipping fifteen services is one
deployment, not fifteen — attributing it to each component would report a deployment frequency the
organization does not have.

A version that shipped inside a package has **no deployment of its own**. Looking up its deployments
returns nothing; find the package whose manifest names it. This is the most common source of "why is
this empty?".

Outcomes are one-way: once `Deployments_Succeed` or `Deployments_Fail` is recorded, neither can be
called again, and the only step left is `Deployments_RollBack` on a success. A rollback is final.
**Failure and rollback are different**: a failure never arrived, while a rollback arrived and had to
be undone. Change failure rate counts the second kind.

**A production success releases what it shipped.** Succeeding a production deployment, or importing one
that succeeded or was rolled back, marks an unreleased version — or a package and the versions that
changed in it — Released at the deployment's completion. So where deployments are recorded, you do not
also need `Versions_MarkReleased` or `ReleasePackages_MarkReleased`. A released moment already set is
never replaced: import history oldest first, or fix it afterwards with `Versions_CorrectDates` or
`ReleasePackages_CorrectDates`.

---

## Typical flows

### Recording what shipped, end to end

1. `Versions_Plan` — the artifact that was built, against a **releasable** product.
2. `Versions_Cut`, then `Versions_MarkReleased`. For history entered after the fact, pass the past
   moments as `cutAt` and `releasedAt`; cutting is optional, and `Versions_MarkReleased` works on a
   version never cut. Use `Versions_CorrectDates` only to fix a moment already recorded wrongly — it
   moves no status, and it clears any date you do not resend.
3. `ReleasePackages_Assemble` — where several components shipped together. Name the **version record**
   on each manifest line, not just the version string, or the release will not know the version is
   already inside a package.
4. `Releases_Plan` — the announcement. Usually under a product **line**, or no product at all.
5. `Releases_SetContents` — the packages and any directly-carried versions, both lists complete.
6. `Releases_MarkReleased` — through the announcing checklist above.
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

`Versions_GetVersions` — unshipped versions come first. This is a version question, not a release one.

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
