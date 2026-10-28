---
"@wayd/mcp": minor
---

Intake requests now notify their people. `Me_GetNotifications` carries the reasons `Requester`, `Assigned` and `Participant`, and the events `Withdrawn`, `Retitled`, `DetailsChanged`, `AssigneeChanged`, `ParticipantsChanged` and `ChannelChanged`; `actor` is null on a request whose details you cannot see. `Me_MuteReason` now accepts `Requester`, `Assigned` and `Participant`.
