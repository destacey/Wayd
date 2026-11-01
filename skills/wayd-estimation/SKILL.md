---
name: wayd-estimation
description: Read before calling the Wayd MCP tools named PokerSessions_* or EstimationScales_*. Guides agents running Wayd planning poker via the Wayd MCP server — poker sessions, rounds, voting, revealing and resetting votes, recording a consensus estimate, completing a session, and the estimation scales sessions vote on, including activating, deactivating and customizing them. Use when listing or reading a poker session or its results, creating, renaming or deleting a session, adding, labelling, describing or removing rounds, casting or withdrawing the user's own vote, revealing or resetting a round, setting a round's consensus, completing a session, or listing, creating, changing, activating, deactivating or deleting an estimation scale.
---

# Wayd Planning Poker and Estimation Scales

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

Planning poker sits behind the `planning-poker` feature flag: when it is off every `PokerSessions_*` and `EstimationScales_*` call answers 404 — tell the user the feature is not enabled rather than that nothing exists.

## Entity context

```
Estimation scale (UUID or key)       the cards a session votes with
└── Version (UUID, number)           one published set of values; never changes
Poker session (UUID or key)          Active → Completed, pinned to one scale version
└── Round (UUID)                     one item being estimated: Voting → Revealed → Accepted
    └── Vote                         one per participant, the participant's chosen value
```

### Estimation scale

- Has a `name`, optional `description`, `isActive`, and a `currentVersion`: the version new sessions vote on.
- **A version** has a `number` (1, 2, …) and an ordered list of `values`, each a `value` of at most 32 characters, unique on the version, and `isEstimate`. **`isEstimate: false` marks a card** — such as `?`, `∞` or `☕` — that can be voted but never becomes a size. A version needs at least 2 estimates. A published version never changes.
- **Changing a scale's values publishes the next version** (a value added, removed, renamed or reordered, or a flag flipped). Renaming the scale, editing its description, activating or deactivating it publish nothing.
- Wayd seeds four scales on a new installation — Fibonacci, Modified Fibonacci, T-Shirt Sizes and Simple — each ending in the cards `?`, `∞` and `☕`. They are ordinary scales: they can be edited, deactivated or deleted like any scale an organisation adds, so do not promise a user that a "built-in" scale is fixed.
- Ids are **UUIDs**; reads also take the integer `key`.
- **Active status** decides what is offered: `EstimationScales_GetScales` lists only active scales unless `includeInactive: true`, and a new session must be given an active scale — an inactive one is refused. Deactivating a scale leaves the sessions that use it untouched: they keep voting on their version. At least one scale must stay active.

### Poker session

- Has a `name`, a facilitator (whoever created it), one estimation scale and its rounds.
- **Is pinned to a scale version** (`estimationScaleVersion`): the scale's current version when the session was created. It stays there when the scale later publishes a new version — votes and the consensus are always checked against the session's own version.
- `status` is `Active` or `Completed`. **A completed session is read-only and cannot be reopened**: no rounds, votes, reveals, resets, consensus or label changes, and no rename.
- The scale can change only while the session has no rounds, and only to an active scale; changing it pins the session to that scale's current version.

### Round

| Status | Meaning | What can happen next |
| --- | --- | --- |
| **Voting** | Accepting votes; only the user's own value is returned | submit/withdraw a vote, reveal, reset |
| **Revealed** | Votes are shown; voting is closed | set consensus, reset |
| **Accepted** | `consensusEstimate` recorded | reset (clears votes and the estimate) |

A round has an optional `label` (at most 512 characters) naming what is estimated, such as a story's title or key, and an optional `description` (at most 2048 characters) with detail for the team, such as acceptance criteria. Rounds are ordered by when they were added.

### Votes are personal

- `PokerSessions_SubmitVote` and `PokerSessions_WithdrawVote` act **as the signed-in user** — there is no way to vote for someone else. Cast a vote only when the user tells you their own value; never choose one for them.
- Submitting again replaces the user's earlier vote in that round.
- A vote's value must be one of the values on the session's version exactly as written — cards such as `?` included when the version has them; anything else is refused. Read them from `estimationScaleVersion.values` in `PokerSessions_GetSession`, not from the scale's current version, which may be newer.
- **Unrevealed votes are hidden.** While a round is Voting, `PokerSessions_GetSession` returns every vote's `value` as an empty string except the user's own. Report who has voted (`voteCount`, participants), not that they voted blank; everyone's values appear once the round is revealed.

---

## Instructions

### Reading

| Goal | Tool | Notes |
| --- | --- | --- |
| List sessions | `PokerSessions_GetSessions` | Optional `status`: `Active` or `Completed`. Each entry has `roundCount`. |
| Read a session and its rounds | `PokerSessions_GetSession` | `idOrKey`. Includes the scale, the `estimationScaleVersion` it votes on with its values, and each round's status, votes and `consensusEstimate`. |
| List scales | `EstimationScales_GetScales` | Active only, unless `includeInactive: true`. Each has its `currentVersion`. |
| Read a scale | `EstimationScales_GetScale` | `idOrKey`; works for inactive scales too. Adds `versions`, every published version newest first. |

**Summarising a session's results:** read the session and list its rounds in order with label and `consensusEstimate`. Call out rounds still Voting or Revealed (no agreed estimate yet). For a revealed round, show the spread of votes — the lowest and highest values and who cast them are what a team discusses before agreeing.

### Running a session

1. **Pick a scale** — `EstimationScales_GetScales`; confirm the scale with the user.
2. **Create the session** — `PokerSessions_CreateSession` with `name` and `estimationScaleId`. The user becomes its facilitator.
3. **Add a round per item** — `PokerSessions_AddRound` with a `label`. Returns the round with its `id`.
4. **Vote** — each participant votes in the Wayd app or through their own agent; you submit only the user's own vote (`PokerSessions_SubmitVote`, body `{ "value": "5" }`).
5. **Reveal** — `PokerSessions_RevealRound` when the facilitator says so. Check `voteCount` first and tell the user if people have not voted yet.
6. **Agree** — `PokerSessions_SetConsensus` with the agreed `estimate`, which must be a value on the session's version. A card such as `?` is allowed — a team that cannot size an item is a real outcome; say so when reporting it rather than treating it as a size. The round must be Revealed; it becomes Accepted.
7. **Re-vote** — `PokerSessions_ResetRound` clears every vote and any consensus and returns the round to Voting. Say so before calling it: the votes are gone for good.
8. **Finish** — `PokerSessions_CompleteSession`. Make sure every round the user cares about is Accepted first; a completed session cannot be changed. Completing deletes empty rounds (no label, description or votes), so do not report those as missing afterwards.

Other session changes:

- `PokerSessions_UpdateSession` — **whole-record overwrite** of `name` and `estimationScaleId`; send the current scale id to keep it and the version the session is on.
- `PokerSessions_UpdateRoundLabel` — replaces the label; omitting it clears it.
- `PokerSessions_UpdateRoundDescription` — replaces the description, leaving the label alone; omitting it clears it.
- `PokerSessions_RemoveRound` — deletes a round with its votes and consensus.
- `PokerSessions_DeleteSession` — deletes the session with every round, vote and estimate, whatever its status. To keep the results, complete it instead.

Write tools take UUIDs: the session's `id` and the round's `id` from `PokerSessions_GetSession`.

### Managing estimation scales

| Goal | Tool | Notes |
| --- | --- | --- |
| Create | `EstimationScales_CreateScale` | `name`, optional `description`, `values` in display order, each `{ "value": "5", "isEstimate": true }`. Starts active with these values as version 1. Returns `id` and `key`. |
| Change | `EstimationScales_UpdateScale` | **Whole-record overwrite** of `name`, `description` and `values`; the body repeats the id as `id`. Changed values publish the next version. |
| Retire or bring back | `EstimationScales_SetActiveStatus` | Body `{ "id": <id>, "isActive": false }`. Fails if the scale already has that status, or if it is the last active scale. |
| Delete | `EstimationScales_DeleteScale` | Refused while any session uses the scale, or for the last active scale. |

Before `EstimationScales_UpdateScale`, read the scale and send back every value that should stay, with its `isEstimate` flag — leaving a flag out sends `false` and turns that value into a card. **Changing `values` publishes a new version and touches no existing session**: sessions already on the scale keep voting on their version, and only sessions created afterwards (or a session whose scale is changed before its first round) use the new one. Tell the user that when they expect a running session to pick up the change. Sending the current values unchanged publishes nothing.

Prefer deactivating to deleting: a scale any session has used cannot be deleted at all.
