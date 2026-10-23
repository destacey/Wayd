# Team capacity

Capacity is the time people are allocated to a team, in **person-days**: one person allocated in full for one
working day. Every capacity tool takes the team's UUID `id` and needs the `team-capacity-management` feature
flag; a 404 for a team you know exists means the flag is off — say so rather than reporting the team missing.
These tools are offered by the hosted MCP endpoint (`/mcp`) only.

| Question | Team | Team of teams |
| --- | --- | --- |
| Who gives the team time, and how much, over a range? | `Teams_GetCapacity` | `TeamsOfTeams_GetCapacity` (direct allocations only) |
| The same with every team beneath it rolled up | — | `TeamsOfTeams_GetCapacityRollup` |
| What is left after holidays and time away, by week or month? | `Teams_GetCapacityReport` | `TeamsOfTeams_GetCapacityReport` |
| Day-by-day time per person, to plan a sprint against | `Teams_GetCapacityPlan` | `TeamsOfTeams_GetCapacityPlan` |
| Suggested allocations for the members, to save | `Teams_GetAllocationProposal` | `TeamsOfTeams_GetAllocationProposal` |

## Reading capacity

- **Name the measure.** `allocatedPersonDays` is all allocated time; `deliveryPersonDays` is the part in roles
  that count toward delivery capacity (a team member role setting); `availablePersonDays` in the report is what
  is left after `holidayPersonDays` and `timeAwayPersonDays`. Planning work against a team uses delivery,
  available time.
- **Working days are the team's.** A team counts days in its own working week less its holiday calendar; a team
  of teams counts the system default working week and holidays.
- **Never add a team of teams to its teams.** `TeamsOfTeams_GetCapacity` counts only people allocated to the team
  of teams itself. `TeamsOfTeams_GetCapacityRollup` and the team-of-teams report add every team beneath it for
  the days each was in the hierarchy, counting each allocation once, on the team it was made to.
- **Time away is time lost, never why.** Reports give `timeAwayPersonDays` and the plan `percentAway`; no capacity
  tool returns the reason, and you must not guess one.
- **Ranges are capped.** `from` and `to` are yyyy-MM-dd, inclusive: at most 731 days for capacity, rollup and
  report, 366 for the plan.
- `Teams_GetCapacityPlan` takes `daysOff` (repeatable) for whole-team days off beyond the working week and
  holidays, such as a sprint's team days off: `2026-10-01` for a full day, `2026-10-01:50` for half of one. They
  shape the answer only; nothing is saved.

## Changing allocations

No team tool changes an allocation. Allocations are per person: change them with `Employees_ChangeAllocations`
(see the `wayd-employees` skill), and confirm with the user first. `canManage` in the capacity response says
whether the caller may.

`*_GetAllocationProposal` suggests a row per active member and active role from `from` (yyyy-MM-dd), splitting
what is left of each member's time across all teams evenly between their roles. It saves nothing: show the user
the rows, then send the ones they keep to `Employees_ChangeAllocations`. A member with nothing left is listed at
0; a member already allocated to the team on `from` gets no rows.
