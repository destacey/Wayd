---
'@wayd/mcp': minor
---

Add create and update tools for portfolios, programs, and projects, completing PPM record management.

Creates: `Portfolios_Create`, `Programs_Create`, `Projects_Create`. New records start in Proposed — creating one does not activate it.

Updates: `Portfolios_Update`, `Programs_Update`, `Projects_Update`, plus `Projects_ChangeProgram` (move a project between programs, or pass null to detach it) and `Projects_ChangeKey`. Also fills three gaps found along the way: `Projects_UpdateProjectHealthCheck`, `Projects_DeleteProjectHealthCheck`, and `ExpenditureCategories_GetOptions`, which resolves the category id that project create and update both require.

**Updates are whole-record overwrites, not patches**, matching the API's `PUT` semantics. A field omitted from the body is cleared rather than preserved, so every update tool tells the caller to read the record first and echo back what should stay the same.

The sharp edge is role assignments. `sponsorIds`, `ownerIds`, `managerIds`, and `memberIds` replace the membership for that role, and a list that is omitted **or empty** removes everyone currently holding it — there is no way to express "leave this role alone". A minimal-looking `Projects_Update` carrying only a new name will strip every sponsor, owner, manager, and member. That is worth stating plainly because Owners and Managers are precisely who is authorised to manage PPM records, so an incautious update can leave a project nobody but a domain administrator can edit. The warning appears on every role-list field, in each update tool's description, and in the `wayd-ppm` skill.

Updates and the two project-identity changes are annotated `destructiveHint` so clients confirm first; creates are not, since they add a record rather than overwriting one. Deleting a portfolio, program, or project remains unavailable through MCP.

Also fixes a latent bug in the stdio test harness. It counted newline-split segments to decide when the server had finished responding, so a chunk ending mid-line counted as a complete response and the child was killed partway through writing. The tools/list payload only recently grew past the pipe buffer, which is what exposed it; the harness now counts newline-terminated lines only.
