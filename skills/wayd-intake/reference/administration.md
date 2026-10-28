# Administering Demand Management

Desks, channels and tag axes are shared by everyone who submits and reviews requests. Confirm every change with the user first, as the main skill says.

Who may do what:

- **Demand Management Admin** (the Administer permission on intake requests) — creates and deletes desks, chooses a desk's visibility and its Desk Admins, and keeps the company's channels and tag axes.
- **Desk Admin** — changes their own desk's details, settings, agents, channels, tag axes and workflow. The Demand Management Admin can do this on every desk too.

## Desks

- **List and read**: `IntakeDesks_GetList` (all desks; `submittableOnly` for those taking requests now) and `IntakeDesks_GetIntakeDesk` (UUID or key). `waiting` is filled only for the desk's members and the Demand Management Admin. `canManage`, `canAdminister` and `canDelete` say what the caller may change.
- **Lookups**: `IntakeDesks_GetVisibilities` (`Open`, `SummaryOnly`, `Private`) and `IntakeDesks_GetRoles` (`Admin`, `Agent`).
- **Create** with `IntakeDesks_Create`: `name` (unique), `description` — written for requesters, saying what kind of ask to bring here, since they choose a desk by it — `visibility`, `isRequesterSelectable`, `isOpen`, `requesterMayAddParticipants` and its `members`, each an employee id with a `role`. Give it at least one Desk Admin, or nobody but the Demand Management Admin can run it.
- **Update** with `IntakeDesks_Update`, which **overwrites the desk's settings and its whole member list**:
  1. Read the desk with `IntakeDesks_GetIntakeDesk`.
  2. Send back every setting and **every member** that should stay, changing only what the user asked. A member left out is removed from the desk and unassigned from its requests.
  3. Send its `revision` as `expectedRevision`. A 409 means someone changed the desk since: read it again and redo the change on the new version, never resend the old body.
  4. `offersAllChannels`/`channelIds` and `offersAllTagCategories`/`tagCategoryIds` may be left out to keep the desk's choice as it is. With `offersAllChannels: false`, an empty `channelIds` means the desk asks no channel at all.
  Changing `visibility` or any Desk Admin needs the Demand Management Admin. Making a desk less private exposes its requests to more people: say so.
- **Workflow**: `IntakeDesks_GetWorkflow` shows the workflow a desk follows and whether it may change; `IntakeDesks_GetWorkflowChoices` lists the published intake workflows; `IntakeDesks_ChangeWorkflow` points the desk at one, only while no request is on it. Building workflows is done in Wayd's settings.
- **Delete** with `IntakeDesks_Delete`, only while no request is on it. A desk with requests is closed instead (`isOpen: false` through an update).

## Channels

The company's one list of where requests come from; each desk offers some or all of it.

- `IntakeChannels_GetList` lists them in order (`includeArchived` for archived ones; `deskId` for those a desk offers).
- `IntakeChannels_Create` adds one at the end (`name` unique, `description` shown to whoever picks one). Desks offering every channel offer it at once; desks that chose theirs do not.
- `IntakeChannels_Update` renames it or changes its description; every request carrying it reads the new name.
- `IntakeChannels_Archive` / `IntakeChannels_Restore`: channels are never deleted. Requests keep an archived channel.
- `IntakeChannels_Reorder` takes **every** channel id, archived included, exactly once, in the new order: list them first and send the whole list.

## Tag axes and tags

Reviewers file requests under tags, grouped on axes (Area, Segment…). One company set serves every desk.

- `IntakeRequestTagCategories_GetList` lists the axes in order with their tags (`includeInactive`; `deskId` for those a desk uses). Each tag's `requestCount` counts only the requests the caller can see.
- `IntakeRequestTagCategories_Create`: `name`, `description` and `allowsMany`, which **cannot change later** — when false, tagging a request replaces its tag on that axis. Ask the user which they want.
- `IntakeRequestTagCategories_Update`, `IntakeRequestTagCategories_SetActive`, `IntakeRequestTagCategories_Reorder` (every axis, inactive included, exactly once) and `IntakeRequestTagCategories_Delete` (only while no request is tagged along it; deactivate it otherwise).
- Tags on an axis: `IntakeRequestTagCategories_AddTag`, `IntakeRequestTagCategories_UpdateTag`, `IntakeRequestTagCategories_SetTagActive` and `IntakeRequestTagCategories_DeleteTag` (only while no request carries it; deactivate it otherwise).
- A system axis cannot be renamed, deleted or have its tags changed.

Deactivating an axis or a tag stops it being applied; requests that carry it keep it.
