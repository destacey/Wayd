# Lifecycle: withdraw versus revert

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
once the record is released or withdrawn.

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
