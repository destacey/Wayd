---
"@wayd/mcp": minor
---

Notifications about a project, program or PI objective you hold a role on, or whose team you belong to, now carry the reason `Role` instead of `Participant`. Mute that reason with `Me_MuteReason` and `Role`; `Participant` is refused until intake requests send it. Existing mutes of `Participant` were moved to `Role`, so nobody's mutes change. `Desk` is a new reason, defined for intake requests but not sent yet.
