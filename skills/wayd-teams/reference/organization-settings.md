# Holiday calendars, team member roles and team types

The organization-wide settings teams are built on. Confirm with the user before each change: a change here
reaches every team and person that uses the setting.

## Holiday calendars

A holiday calendar is a shared list of days off, such as a country's public holidays. A team takes one through
its operating model and a person through `Employees_ChangeHolidayCalendar`; either with none takes the system
default calendar (`isDefault`). Holidays take those days out of capacity.

| Task | Tool |
| --- | --- |
| List calendars | `HolidayCalendars_GetList` |
| A calendar with its holidays and usage | `HolidayCalendars_GetHolidayCalendar` (UUID or key) |
| What changed, and who changed it | `HolidayCalendars_GetActivities` |
| Create an empty calendar | `HolidayCalendars_Create` |
| Rename or redescribe | `HolidayCalendars_Update` |
| Delete | `HolidayCalendars_Delete` |
| Add, move or rename, remove a holiday | `HolidayCalendars_AddHoliday`, `HolidayCalendars_ChangeHoliday`, `HolidayCalendars_RemoveHoliday` |

- Calendar names are unique. A calendar holds at most one holiday per date; a holiday is a `date` (yyyy-MM-dd)
  and a `name`, and `holidayId` is its `id` from the calendar.
- **Update overwrites name and description:** an omitted description is cleared.
- **Delete is refused** while the calendar is the system default, or while any team operating model (past or
  current) or employee uses it. `operatingModelCount` and `employeeCount` on the calendar say how many; move them
  to another calendar first. Deleting cannot be undone.

## Team member roles

The roles people hold on teams, such as Developer or Scrum Master. A role's `countsTowardDeliveryCapacity` says
whether time allocated in it adds to a team's delivery capacity.

| Task | Tool |
| --- | --- |
| List roles (active only unless `includeInactive`) | `TeamMemberRoles_GetList` |
| One role | `TeamMemberRoles_GetRole` |
| Create | `TeamMemberRoles_Create` |
| Update | `TeamMemberRoles_Update` |
| Retire or bring back | `TeamMemberRoles_Deactivate`, `TeamMemberRoles_Activate` |
| Delete | `TeamMemberRoles_Delete` |

- Role names are unique. A new role counts toward delivery capacity unless you send
  `countsTowardDeliveryCapacity: false` (suits a role whose time on a team does not burn its planned work).
- **Update overwrites name and description** (an omitted description is cleared); an omitted
  `countsTowardDeliveryCapacity` is left as it is. Changing it changes every team's delivery capacity for time
  allocated in the role, for past ranges too: say so before calling.
- **Delete is refused** while any team member holds the role or any allocation uses it. Deactivate it instead:
  it leaves the role on the people who hold it and drops it from lists and allocation proposals.

## Team types

`TeamTypes_GetList` lists the two fixed kinds of team, Team and Team of Teams, with their descriptions. They
cannot be changed.
