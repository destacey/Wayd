---
name: wayd-intake
description: Read before calling the Wayd MCP tools named IntakeRequests_*, IntakeDesks_*, IntakeChannels_*, IntakeRequestTagCategories_* or IntakeEmployeeOptions_*. Guides agents working with Wayd intake requests via the Wayd MCP server — raising a request for the user or on someone's behalf, finding out where a request stands and why, reviewing requests on a desk (status changes, assignment, tags, participants, moving a request to another desk, recording the projects a request became), and setting up desks, channels and tag axes. Use when the user asks to raise, submit or log a request for delivery capacity, asks what happened to a request or which of their requests are open, names a request by its number, triages or decides requests on a desk they work, or administers Demand Management.
---

# Wayd Intake Requests

The intake tools are offered by the hosted MCP endpoint (`/mcp`) only, in its `intake` toolset. They answer 404 while the organization has Demand Management turned off (the `demand-management` feature flag); say so rather than retrying.

An **intake request** is one ask for delivery capacity that is not yet a project or a work item. It is reviewed on one **desk** — a review responsibility, not a team — whose **members** are **Agents**, who act on its requests, and **Desk Admins**, who also run the desk. Being named on a desk is all it takes; working one desk gives nothing on another. The **Demand Management Admin** acts as a Desk Admin on every desk. Every request moves through its desk's review **workflow** of statuses.

## Reference

Read the matching file before working in these areas. The rules on this page still apply there.

| File | Read it when |
| --- | --- |
| [reference/administration.md](reference/administration.md) | Creating, changing or deleting a desk or its members and workflow, or managing channels, tag axes and tags |

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up stays undone unless they ask for it.

## Identifiers and people

A request has a UUID `id` and a sequential number, its `key`. Tools whose parameter is `idOrKey` take either; every other tool takes the UUID, so look the request up first. Desks likewise have an `id` and a `key`.

Requester, participant, assignee, member and `committedById` fields take **employee ids**. `IntakeEmployeeOptions_GetList` lists the active employees a request can name, by name, and needs no permission to view employees: filter it once for everyone you need. If several match a name, ask which one. For the caller themselves, use `Me_GetProfile` (see the `wayd-users` skill).

## Submitting a request

`IntakeRequests_Submit` needs the caller's account to be linked to an employee: that employee is recorded as the submitter, and as the requester unless `requesterId` names someone else. If the call is refused because the account is not linked, tell the user to ask an administrator to link it; there is no way around it.

1. **Choose the desk.** Call `IntakeDesks_GetList` with `submittableOnly: true` — only open desks a requester can pick. Match the ask against each desk's `description`, which says what kind of ask it takes; never choose by the desk's name or its members. If one desk fits, say which. If several could, ask the user, quoting the descriptions. If none is returned, nothing can be submitted now. The choice only decides who looks first: the desk can move the request.
2. **Choose the channel.** Call `IntakeChannels_GetList` with the desk's `deskId`. If it returns channels, `channelId` is required: ask the user where the request came from (a customer, support, a stakeholder…) and match it to a channel's name and description. Do not guess one. If it returns none, leave `channelId` out.
3. **Write the ask.** `title` is one line, at most 256 characters, and on most desks **everyone who can view requests reads it**, even where the detail stays with the desk: tell the user, and keep confidential names, customers and figures out of it. `description` is the ask in full and is required. `expectedBenefits` — what gets better, and for whom — is optional but is what reviewers weigh when capacity is short, so ask for it.
4. **The requester.** Leave `requesterId` out when the user is asking for themselves. When they submit for someone else, pass that person's employee id; both of them can then follow the request, and only the requester can withdraw it.
5. **A need-by date, only if there is one.** Never invent a date or a basis. A `needBy` carries all of:
   - `date` (`yyyy-MM-dd`)
   - `basis` — `Regulatory`, `Contractual` or `ExternalEvent` when something outside the company sets the date; `Commitment` or `RequesterPreference` when someone here chose it. `IntakeRequests_GetNeedByBases` lists them with their families, and `IntakeRequests_GetNeedByBasisFamilies` names the families
   - `reference` — what the date rests on: the regulation and clause, the contract and clause, the event, or a note
   - for `Commitment` only, and then required: `committedById` (the employee who made the commitment) and `committedOn` (when). Any other basis refuses them
6. **Confirm, then submit.** Show the user the desk, channel, title, description, benefits, requester and need-by as they will be sent, and submit once they agree. Report the request's number (`key`) from the result. It starts in the status of the desk's workflow that means Submitted, whatever the desk calls it.

Before raising a request, it can be worth checking whether the ask already exists: `IntakeRequests_GetList` with `openOnly: true` lists the open requests the caller can see. It has no search, so scan the titles in the result.

## Finding requests

`IntakeRequests_GetList` with `mine: true` lists the requests the caller asked for or submitted, including those entered on someone else's behalf; add `openOnly: true` for those still under review or accepted, and `deskId` for one desk. Without `mine`, it lists every request the caller can see: their own and those on desks they work, plus, with the permission to view requests, those other desks let everyone see. Each item is a summary: number, title, status, desk, requester, channel, assignee, need-by and tags, with `submittedOn` and `statusChangedOn` for how long it has waited.

For a desk's queue, pass its `deskId` and `openOnly: true`, then filter the result: no `assignee` is what still needs triage; `needBy` with a fixed basis is what cannot move.

## Where a request stands

`IntakeRequests_GetIntakeRequest` takes the request's UUID or number. Read the outcome from:

- `summary.status` — its `name` is what the desk calls the stage; its `category` is what it means, whatever the name:

  | Category | Means |
  | --- | --- |
  | `Proposed` | Open: received and still being reviewed, or waiting on the requester |
  | `Active` | Open: accepted for delivery |
  | `Done` | Delivered |
  | `Removed` | Not going ahead: declined, a duplicate, withdrawn, or a desk's own stage such as deferred |

- `substance.statusReason` — why it is in that status: the desk's reason for declining, deferring, finishing or marking it a duplicate, or the requester's note on withdrawing it. Null when the move needed none.
- `duplicateOf` — the request it gave way to, when it is a duplicate and the caller can see that request. Look that one up to say where the ask went.
- `outcomes` — what an accepted request became: each a `NewProject` (started for it) or `ExistingProjectScope` (an existing project that absorbed it, with a `note` saying what), naming the `project` by key and name. `project` is null when that project has since been deleted. Everyone who sees the request sees its outcomes. To answer "what happened to my ask" past the decision, look the project up (see the `wayd-ppm` skill).
- `summary.assignee` — the desk member who owns it, if anyone does. An owner, not an approver.
- The `can*` flags — what the caller may do to it now (`canEdit`, `canChangeStatus`, `canWithdraw`, …). Offer only what they allow.

For the path it took, `IntakeRequests_GetStatusHistory` lists every status change, newest first, with the status names as they were at the time; `IntakeRequests_GetStatusWorkflow` is the workflow those statuses belong to. `IntakeRequests_GetActivities` is the full history of changes, paged.

### What the caller cannot read

A request is read in tiers, and the tools return only the tiers the caller may read:

- **`substance` is null** when the caller may not read the detail: on a summary-only desk, for someone who is not one of the request's people or the desk's. Say the detail is not visible to them; never describe it as empty. The status history and activity then come without reasons or who made each change.
- **A request answers 404** both when it does not exist and when the caller may not see it, such as one on a private desk they are not part of. Never say which.
- **The desk's internal notes and triage reasoning reach only the desk's agents** and the Demand Management Admin, never anyone outside the desk. Never repeat them to the requester or a participant, and do not speculate about why a desk decided something beyond `statusReason`.

A gap in request numbers can be requests the caller cannot see; it says nothing about them.

## The requester's own changes

- **Edit** with `IntakeRequests_Update` while the request is Proposed (an agent on its desk can edit it while it is open). It **overwrites the whole request**: read it first and send back `title`, `description`, `expectedBenefits`, `channelId` and `needBy` as they are, changing only what the user asked. An omitted `needBy` clears the date, its basis and reference. Send the request's current `channelId` unless the user is changing it: a recorded channel can be changed but not cleared while the desk offers channels, so an omitted one is refused.
- **Withdraw** with `IntakeRequests_Withdraw`, with an optional `note` for the desk. Only the requester can, while it is open — not whoever submitted it on their behalf. It stays on record as Withdrawn, and an agent can reopen it.
- **Give someone access** with `IntakeRequests_AddParticipant` (an employee id): participants read the request but cannot change it. The requester and submitter can, unless the desk is private and does not allow it (`canAddParticipants`).
- **Follow it** with `Me_Follow` (`ownerType` `demand.intake-request` and the request's UUID; see the `wayd-users` skill), where the `following` feature flag is on. Following an intake request sends no notifications yet.

## Reviewing requests on a desk

These need the caller to be an Agent or Desk Admin on the request's desk, or the Demand Management Admin, with an account linked to an employee; `isDeskAgent` and the `can*` flags on `IntakeRequests_GetIntakeRequest` say whether they may.

- **Change its status.** Call `IntakeRequests_GetStatusOptions` first: the statuses it can move to, in workflow order, each with `needsReason` and `needsSurvivingRequest`. Pass the option's `id` as `statusId` to `IntakeRequests_ChangeStatus`. A `reason` is required for declining, marking a duplicate, and finishing a request that has no outcome, and **the requester reads it**: draft it for them, plainly, and have the user approve the wording. A duplicate also needs `duplicateOfId`, a request the caller can see that is not itself a duplicate. Withdrawn is never an option; only the requester withdraws.
- **Record what it became** with `IntakeRequests_AddOutcome` while the request is accepted (`canChangeOutcomes`): `kind` `NewProject` or `ExistingProjectScope`, the `projectId`, and for existing project scope a `note` saying what the project absorbed. Find the project with the PPM tools; one created moments ago may answer "not found" for a short while. Recording an outcome never changes the project, and a request points at a project once. `IntakeRequests_RemoveOutcome` takes one off, by the project's id, also only while accepted. To list the requests behind a project, call `IntakeRequests_GetProjectIntakeRequests` with its id: requests the caller may not see are left out without a trace, so never say how many there might be.
- **Assign it** with `IntakeRequests_ChangeAssignee`: a member of the desk, or `null` to unassign. The assignee owns the request; they do not approve it.
- **Move it to another desk** with `IntakeRequests_Reroute` while it is open. The destination must be open to new requests but need not be one the caller works or one requesters can pick. Call `IntakeRequests_GetArrivalStatuses` with the destination `deskId` first: `defaultStatusId` is where it lands unless you pass a `statusId` from `statuses`. A move to a less private desk is refused unless `confirmWiderVisibility` is true — only set it after telling the user that more people will be able to read the request. An assignee who is not on the destination desk is unassigned. Moving never changes the channel.
- **Tag it** with `IntakeRequests_Tag` and `IntakeRequests_Untag`, along the axes the desk uses (`IntakeRequestTagCategories_GetList` with `deskId`). On an axis that takes one tag, tagging **replaces** the tag the request had there: say so before you do it.
- **Remove a participant** with `IntakeRequests_RemoveParticipant`. What they already read stays read.
- **Delete it** with `IntakeRequests_Delete` — a Desk Admin or the Demand Management Admin only, only while it has never left its first status and no other request is a duplicate of it (`canDelete`). It is a last resort and cannot be undone: offer to decline it instead, which keeps the decision on record.
