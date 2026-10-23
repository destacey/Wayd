# Risks

A risk belongs to one team or team of teams. It is Open or Closed, sits in a ROAM category, and has an impact and
a likelihood that together give its exposure. The tools that write risks are offered by the hosted MCP endpoint
(`/mcp`) only.

## Finding risks

| Question | Tool |
| --- | --- |
| Every team's risks | `Risks_GetList` |
| One team's risks | `Teams_GetRisks` |
| A team of teams' own risks (not its teams') | `TeamsOfTeams_GetRisks` |
| Risks of the teams in a planning interval | `PlanningIntervals_GetRisks` (see the `wayd-pi` skill) |
| Open risks assigned to me | `Risks_GetMyRisks` |
| One risk, by UUID or key | `Risks_GetRisk`, or `Teams_GetRisk` / `TeamsOfTeams_GetRisk` scoped to its team |
| What changed on a risk, and who changed it | `Risks_GetActivities` |

- **Lists return open risks** unless `includeClosed` is true. Say which you asked for.
- **`Risks_GetMyRisks` is scoped to the caller.** It finds open risks assigned to the employee linked to the
  caller's account, and is empty for an account with no linked employee — say that rather than "you have no
  risks".
- **Exposure is derived, not set.** Impact and likelihood are each Low (1), Medium (2) or High (3); exposure is
  Low when their ids sum below 4, Medium at exactly 4, High above. List results give the names only; the detail
  tools give each as `{ id, name }`.

## Creating and updating

Confirm with the user before each call.

| Task | Tool |
| --- | --- |
| Create a risk for any team or team of teams | `Risks_Create` (or `Teams_CreateRisk` / `TeamsOfTeams_CreateRisk`) |
| Update, close or reopen a risk | `Risks_Update` (or `Teams_UpdateRisk` / `TeamsOfTeams_UpdateRisk`) |
| Status, category and grade ids | `Risks_GetStatuses`, `Risks_GetCategories`, `Risks_GetGrades` |

- **The lookups are numeric ids, not codes.** `statusId`: 1 Open, 2 Closed. `categoryId` (ROAM): 1 Resolved,
  2 Owned, 3 Accepted, 4 Mitigated. `impactId` and `likelihoodId`: 1 Low, 2 Medium, 3 High. The lookup tools give
  each with its description.
- **`teamId` is required** and is the UUID of the team or team of teams that owns the risk. A risk cannot be
  moved to another team: an update must send its current team.
- **The reporter is the caller.** Creating a risk needs the caller's account to be linked to an employee; a
  refusal for that is not a problem with the risk. A new risk is always Open.
- **Update is a whole-record overwrite.** An omitted description, assignee, follow-up date or response is
  cleared. Read the risk first and pass back every field that should stay, changing only what the user asked.
- **Closing is an update.** Send `statusId` 2 with every other field unchanged; that stamps the closed date.
  Sending 1 reopens it and clears the date.
- `assigneeId` is an employee UUID (resolve a name with `Employees_GetList`); `followUpDate` is yyyy-MM-dd;
  `response` is what is being done about the risk.
