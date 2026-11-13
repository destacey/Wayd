# Administering Demand Management

Desks, channels and tag categories are shared by everyone who submits and reviews requests. Confirm every change with the user first, as the main skill says.

Who may do what:

- **Demand Management Admin** (the Administer permission on intake requests) — creates and deletes desks, chooses a desk's visibility and its Desk Admins, and keeps the company's channels and tag categories.
- **Desk Admin** — changes their own desk's details, settings, agents, triagers, channels, tag categories, scoring model, estimation scale and workflow. The Demand Management Admin can do this on every desk too.

## Desks

- **List and read**: `IntakeDesks_GetList` (all desks; `submittableOnly` for those taking requests now) and `IntakeDesks_GetIntakeDesk` (UUID or key). `waiting` is filled only for the desk's members and the Demand Management Admin. `canManage`, `canAdminister` and `canDelete` say what the caller may change.
- **Lookups**: `IntakeDesks_GetVisibilities` (`Open`, `SummaryOnly`, `Private`) and `IntakeDesks_GetRoles` (`Admin`, `Agent`).
- **Create** with `IntakeDesks_Create`: `name` (unique), `description` — written for requesters, saying what kind of ask to bring here, since they choose a desk by it — `visibility`, `isRequesterSelectable`, `isOpen`, `requesterMayAddParticipants` and its `members`, each an employee id with a `role`. Give it at least one Desk Admin, or nobody but the Demand Management Admin can run it.
- **Update** with `IntakeDesks_Update`, which **overwrites the desk's settings and its whole member list**:
  1. Read the desk with `IntakeDesks_GetIntakeDesk`.
  2. Send back every setting and **every member** that should stay, changing only what the user asked. A member left out is removed from the desk and unassigned from its requests.
  3. Send its `revision` as `expectedRevision`. A 409 means someone changed the desk since: read it again and redo the change on the new version, never resend the old body.
  4. `offersAllChannels`/`channelIds` and `offersAllTagCategories`/`tagCategoryIds` may be left out to keep the desk's choice as it is. With `offersAllChannels: false`, an empty `channelIds` means the desk asks no channel at all.
  5. `triagerIds` may be left out to keep the desk's triagers. When sent, it is every member who should triage after the change, each also in `members`; empty means nobody, and the desk's new requests then tell nobody. Each member's `isTriager` on the desk says who triages now.
  Changing `visibility` or any Desk Admin needs the Demand Management Admin. Making a desk less private exposes its requests to more people: say so.
- **Triagers** are the members, of either role, told about requests waiting on the desk unassigned: when one arrives or is moved there, when its requester renames it, changes its details or need-by date, or replies, and when it is assigned, withdrawn, closed or moved away. A new desk's starting members all triage; someone added later does not until named in `triagerIds`. When a user asks who hears about new requests on a desk, it is its triagers.
- **Scoring model**: `IntakeDesks_GetScoringModelChoices` lists the active scoring models; `IntakeDesks_ChangeScoringModel` gives the desk one, or `null` to stop scoring its requests. The desk's `scoringModel` names its current one. Recorded scores keep the model they used, and only scores from the desk's current model rank together, so changing it leaves every request needing a re-score before it ranks again; say so before changing it. Building models is done in Wayd's settings.
- **Estimation scale**: `IntakeDesks_GetEstimationScaleChoices` lists the active scales, each with its current version's estimate values; `IntakeDesks_ChangeEstimationScale` gives the desk one, pinned to that version, or `null` to stop estimating its requests. The desk's `estimationScale` names it with its version, and `isCurrentVersion: false` means the scale has a newer one — choosing the same scale again moves the desk to it. Before changing it, call `IntakeDesks_GetOpenEstimates` to see the estimates on the desk's open requests, counted by value and version; when there are any, tell the user and ask which `openEstimates` they want: `Keep` (the default) leaves each on its version until re-estimated, and `Clear` clears every one. Never choose `Clear` without asking. Done and Removed requests keep their estimates either way. Building scales is done in Wayd's settings.
- **Workflow**: `IntakeDesks_GetWorkflow` shows the workflow a desk follows and whether it may change; `IntakeDesks_GetWorkflowChoices` lists the published intake workflows; `IntakeDesks_PreviewWorkflowChange` shows, for a chosen workflow, where each of the desk's statuses lands, how many requests are in each, and the statuses an unplaced one can go to; `IntakeDesks_ChangeWorkflow` points the desk at it and moves every request on the desk, with `decisions` for each status the preview leaves without a target (`to` null) and for any match the user overrides. Always preview first and have the user confirm the mapping and the number of requests moving: a request lands in a Duplicate status only while it names its survivor, and in a closing status needing a reason only while it has one, so a mapping breaking either is refused, naming every request it refused, and nothing moves. A request submitted while the change ran can be left on the old workflow; calling `IntakeDesks_ChangeWorkflow` again with the workflow the desk now follows moves only such requests. Building workflows is done in Wayd's settings.
- **Delete** with `IntakeDesks_Delete`, only while no request is on it. A desk with requests is closed instead (`isOpen: false` through an update).

## Channels

The company's one list of where requests come from; each desk offers some or all of it.

- `IntakeChannels_GetList` lists them in order (`includeArchived` for archived ones; `deskId` for those a desk offers).
- `IntakeChannels_Create` adds one at the end (`name` unique, `description` shown to whoever picks one). Desks offering every channel offer it at once; desks that chose theirs do not.
- `IntakeChannels_Update` renames it or changes its description; every request carrying it reads the new name.
- `IntakeChannels_Archive` / `IntakeChannels_Restore`: channels are never deleted. Requests keep an archived channel.
- `IntakeChannels_Reorder` takes **every** channel id, archived included, exactly once, in the new order: list them first and send the whole list.

## Tag categories and tags

Reviewers file requests under tags, grouped in categories (Area, Segment…). One company set serves every desk.

- `IntakeRequestTagCategories_GetList` lists the categories in order with their tags (`includeInactive`; `deskId` for those a desk uses). Each tag's `requestCount` counts only the requests the caller can see.
- `IntakeRequestTagCategories_Create`: `name`, `description` and `allowsMany`, which **cannot change later** — when false, tagging a request replaces its tag in that category. Ask the user which they want.
- `IntakeRequestTagCategories_Update`, `IntakeRequestTagCategories_SetActive`, `IntakeRequestTagCategories_Reorder` (every category, inactive included, exactly once) and `IntakeRequestTagCategories_Delete` (only while no request is tagged in it; deactivate it otherwise).
- Tags in a category: `IntakeRequestTagCategories_AddTag`, `IntakeRequestTagCategories_UpdateTag`, `IntakeRequestTagCategories_SetTagActive` and `IntakeRequestTagCategories_DeleteTag` (only while no request carries it; deactivate it otherwise).
- A system category cannot be renamed, deleted or have its tags changed.

Deactivating a category or a tag stops it being applied; requests that carry it keep it.

### Free tags

Agents add free tags — tags in no category — by typing a name while tagging, on desks that allow it: `allowsFreeTags` on `IntakeDesks_Update`, off for a new desk. The Demand Management Admin keeps them tidy:

- `IntakeRequestTags_Search` with `isFree: true` lists them with `requestCount`. A free tag is listed only to someone who can see a request carrying it, or to the Demand Management Admin.
- `IntakeRequestTags_UpdateFreeTag` renames one; names are unique among free tags, ignoring case.
- `IntakeRequestTags_Merge` moves every request carrying a free tag, on every desk, to another tag and deletes it — to fold near-duplicates together, or to promote a free tag into a category: add the tag with `IntakeRequestTagCategories_AddTag`, then merge into it. In a category that takes one tag it replaces each request's existing tag there; say so first.
- `IntakeRequestTags_DeleteFreeTag` removes a free tag from every request carrying it, then deletes it — unlike `IntakeRequestTagCategories_DeleteTag`, it does not refuse a tag in use. Say how many requests carry it first.

A free tag nothing carries is deleted on its own, so there is nothing to deactivate.
