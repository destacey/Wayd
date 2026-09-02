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
| **Version** | `Wayd API 4.12.0` | What was **built** — one artifact, cut on a date | Where it went |
| **Release Package** | `WAYD-2026.09.1` | What **moved through environments together** | A product of its own |
| **Release** | `Wayd 2026.09` | What was **announced to customers** | A cut date — it is never cut |

It reads as one sentence with no word doing double duty:

> **Release** 2026.09 shipped **package** WAYD-2026.09.1, containing Wayd API **version** 4.10.0,
> **deployed** to Production.

**Which one am I looking at?** If a customer would recognise the name, it is a *release*. If it names
one artifact and a version number, it is a *version*. If it is what the pipeline pushed, it is a
*package*.

> **The most common error.** Being asked to "record the 4.12.0 release" and calling `Releases_Plan`.
> `4.12.0` names one artifact, so it is a **version** — use `Versions_Plan`. Conversely, "we announced
> 2026.09 last Tuesday" is a **release**, not a version, even though it has a version-looking label.

---

## What can be changed via MCP

Everything in delivery supports create and update, and every lifecycle move is available. **Nothing
in delivery is deletable** — a version, release or package is *withdrawn*, an environment is
*retired*, and a deployment is never removed at all, because the measures read that history.

Every mutating tool is annotated so your client asks before running it. Treat that as a genuine
checkpoint rather than a formality: announcing a release is a statement to customers, and withdrawing
one retracts a statement already made.

---

## Two rules that will refuse you

### A version is announced once, by one route

A release reaches its contents two ways, and may use both:

- **Packages** — the usual route, since a package is the deployment unit.
- **Versions carried directly** — for a single artifact that shipped alone, where nobody assembled a
  package.

**A version shipping inside one of the release's packages cannot also be carried directly.** Otherwise
one release announces the same shipment twice, and "what did 2026.09 contain" has two answers.

The rule is judged against what the release *ends up* containing, not what it contained before — so
moving a version out of the direct list and into a package that ships it works, provided both changes
go in the same `Releases_SetContents` call. A manifest line that names no version record covers
nothing and never conflicts.

### A release cannot be announced while its contents have not shipped

`Releases_MarkReleased` is refused while any version or package the release carries has not shipped.
Telling customers `2026.09` is out while a version inside it has not gone anywhere is the one claim a
release can make that its own contents contradict.

**Before announcing:** call `Releases_GetRelease` and check each contents entry's shipped date. Release
the outstanding ones, or remove them from the release. An **empty release announces normally** — a
repackaging or a pricing change is announced with nothing deployed, and emptiness is never the blocker.

---

## Whole-set replacements

Three tools replace a whole collection rather than adding to one. Sending only what you want to add
silently deletes everything else.

| Tool | Replaces | Sending an empty set |
| --- | --- | --- |
| `Releases_SetContents` | Both routes at once — packages **and** directly-carried versions | Clears the release |
| `ReleasePackages_SetManifest` | Every manifest line | Refused: a package ships at least one component |
| `Releases_Update` / `Versions_Update` | Every descriptive field | An omitted field is cleared |

**Always read the record first** and send back the full intended result. For `Releases_SetContents`
that means calling `Releases_GetRelease`, taking the existing `versions` and `packages`, applying your
change, and sending both complete lists.

---

## Ordering and version numbers

Version numbers and release labels are **free text and never parsed**. `4.8.2` and `2026.04` are both
just labels; Wayd never sorts or compares them. Ordering comes from dates, with an optional
`sequence` override for the case where chronology misleads — a backport shipping after the version
that superseded it.

Do not attempt to infer precedence from a version string, and do not sort results by it.

---

## Lifecycle: withdraw versus revert

Both end an assertion, and choosing wrongly writes a history that misleads whoever reads it later.

| | Withdraw | Revert |
| --- | --- | --- |
| What happened | It really shipped or was announced, then was pulled | It never shipped or was announced; the record was wrong |
| Resulting status | Terminal | Back to a live status |
| The date | Kept — it did happen | Cleared — it did not |
| Reason | Optional | **Required** |

Recording a mistake as a withdrawal leaves the append-only history asserting that somebody pulled
something nobody ever shipped, and a later reader has no way to tell.

**Correcting dates is a third thing.** `Releases_CorrectDates` and `Versions_CorrectDates` say a date
was written down wrongly. The status does not move and the history is untouched — which is why they
exist separately from the actions that assert a record moved.

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

Outcomes are one-way: a deployment that has started is only ever completed, and once `Succeed`, `Fail`
or `RollBack` is recorded, none can be called again. Note that **failure and rollback are different**:
a failure never arrived, while a rollback arrived and had to be undone. Change failure rate counts the
second kind.

---

## Typical flows

### Recording what shipped, end to end

1. `Versions_Plan` — the artifact that was built, against a **releasable** product.
2. `Versions_Cut`, then `Versions_MarkReleased` — or supply both dates later via
   `Versions_CorrectDates` if you are entering history after the fact.
3. `ReleasePackages_Assemble` — where several components shipped together. Name the **version record**
   on each manifest line, not just the version string, or the release will not know the version is
   already inside a package.
4. `Releases_Plan` — the announcement. Usually under a product **line**, or no product at all.
5. `Releases_SetContents` — the packages and any directly-carried versions, both lists complete.
6. `Releases_MarkReleased` — once everything inside has shipped.
7. `Deployments_Start` then an outcome — for each unit that reached an environment.

### Answering "what did we announce in X?"

`Releases_GetReleases` to find it, then `Releases_GetRelease` for its full contents.

### Answering "where did this version go?"

- `Releases_GetReleases` with `containingVersionId` — which announcements carried it, by either route.
- `ReleasePackages_GetReleasePackages` with `containingVersionId` — which packages shipped it.
- `Deployments_GetDeployments` with `versionId` — deployments of the version *itself*, which will be
  empty if it only ever shipped inside a package.

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

A release may name **no product at all** when it spans product lines. Note that filtering releases by
product deliberately excludes those: belonging to no single product, listing one under a product would
misstate what that product announced.
