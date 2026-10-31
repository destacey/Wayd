---
name: wayd-estimation
description: Read before calling the Wayd MCP tools named PokerSessions_* or EstimationScales_*. Guides agents running Wayd planning poker via the Wayd MCP server — poker sessions, rounds, voting, revealing and resetting votes, recording a consensus estimate, completing a session, and the estimation scales sessions vote on, including activating, deactivating and customizing them. Use when listing or reading a poker session or its results, creating, renaming or deleting a session, adding, labelling or removing rounds, casting or withdrawing the user's own vote, revealing or resetting a round, setting a round's consensus, completing a session, or listing, creating, changing, activating, deactivating or deleting an estimation scale.
---

# Wayd Planning Poker and Estimation Scales

## Before any change

Confirm every change with the user before you make it. Before a call that creates, updates, tags, sets, moves, changes the status of, or deletes anything, tell the user what will happen — the record, what it will look like afterwards, and anything replaced or removed with it — and wait for their go-ahead. A request names the goal, not the details you worked out to reach it, and Wayd records are shared by everyone who plans from them, so the user approves the details. Reads need no confirmation.

When the user approves, do exactly what they approved. An option you offered that they did not take up — deleting a record you had just retired, say — stays undone unless they ask for it.

Planning poker sits behind the `planning-poker` feature flag: when it is off every `PokerSessions_*` and `EstimationScales_*` call answers 404 — tell the user the feature is not enabled rather than that nothing exists.

## Entity context

```
Estimation scale (integer id)        the cards a session votes with
Poker session (UUID or key)          Active → Completed
└── Round (UUID)                     one item being estimated: Voting → Revealed → Accepted
    └── Vote                         one per participant, the participant's chosen value
```

### Estimation scale

- An ordered list of `values` (the cards), at least 2, each at most 32 characters, with a `name`, optional `description` and `isActive`.
- Wayd seeds four scales on a new installation — Fibonacci, Modified Fibonacci, T-Shirt Sizes and Simple — each ending in `?`, `∞` and `☕`. They are ordinary scales: they can be edited, deactivated or deleted like any scale an organisation adds, so do not promise a user that a "built-in" scale is fixed.
- Ids are **integers**, not UUIDs.
- **Active status** decides what is offered: `EstimationScales_GetScales` lists only active scales unless `includeInactive: true`, and a new session must be given an active scale — an inactive one is refused. Deactivating a scale leaves the sessions that use it untouched: they keep voting on its values. At least one scale must stay active.

### Poker session

- Has a `name`, a facilitator (whoever created it), one estimation scale and its rounds.
- `status` is `Active` or `Completed`. **A completed session is read-only and cannot be reopened**: no rounds, votes, reveals, resets, consensus or label changes, and no rename.
- The scale can change only while the session has no rounds, and only to an active scale.

### Round

| Status | Meaning | What can happen next |
| --- | --- | --- |
| **Voting** | Accepting votes; only the user's own value is returned | submit/withdraw a vote, reveal, reset |
| **Revealed** | Votes are shown; voting is closed | set consensus, reset |
| **Accepted** | `consensusEstimate` recorded | reset (clears votes and the estimate) |

A round has an optional `label` (at most 512 characters) naming what is estimated, such as a story's title or key. Rounds are ordered by when they were added.

### Votes are personal

- `PokerSessions_SubmitVote` and `PokerSessions_WithdrawVote` act **as the signed-in user** — there is no way to vote for someone else. Cast a vote only when the user tells you their own value; never choose one for them.
- Submitting again replaces the user's earlier vote in that round.
- A vote's value must be one of the session's scale values exactly as written — `?`, `∞` and `☕` included when the scale has them; anything else is refused. Read the values from `PokerSessions_GetSession`.
- **Unrevealed votes are hidden.** While a round is Voting, `PokerSessions_GetSession` returns every vote's `value` as an empty string except the user's own. Report who has voted (`voteCount`, participants), not that they voted blank; everyone's values appear once the round is revealed.

---

## Instructions

### Reading

| Goal | Tool | Notes |
| --- | --- | --- |
| List sessions | `PokerSessions_GetSessions` | Optional `status`: `Active` or `Completed`. Each entry has `roundCount`. |
| Read a session and its rounds | `PokerSessions_GetSession` | `idOrKey`. Includes the scale and its values, and each round's status, votes and `consensusEstimate`. |
| List scales | `EstimationScales_GetScales` | Active only, unless `includeInactive: true`. |
| Read a scale | `EstimationScales_GetScale` | Integer `id`; works for inactive scales too. |

**Summarising a session's results:** read the session and list its rounds in order with label and `consensusEstimate`. Call out rounds still Voting or Revealed (no agreed estimate yet). For a revealed round, show the spread of votes — the lowest and highest values and who cast them are what a team discusses before agreeing.

### Running a session

1. **Pick a scale** — `EstimationScales_GetScales`; confirm the scale with the user.
2. **Create the session** — `PokerSessions_CreateSession` with `name` and `estimationScaleId`. The user becomes its facilitator.
3. **Add a round per item** — `PokerSessions_AddRound` with a `label`. Returns the round with its `id`.
4. **Vote** — each participant votes in the Wayd app or through their own agent; you submit only the user's own vote (`PokerSessions_SubmitVote`, body `{ "value": "5" }`).
5. **Reveal** — `PokerSessions_RevealRound` when the facilitator says so. Check `voteCount` first and tell the user if people have not voted yet.
6. **Agree** — `PokerSessions_SetConsensus` with the agreed `estimate` (normally one of the scale's values). The round must be Revealed; it becomes Accepted.
7. **Re-vote** — `PokerSessions_ResetRound` clears every vote and any consensus and returns the round to Voting. Say so before calling it: the votes are gone for good.
8. **Finish** — `PokerSessions_CompleteSession`. Make sure every round the user cares about is Accepted first; a completed session cannot be changed.

Other session changes:

- `PokerSessions_UpdateSession` — **whole-record overwrite** of `name` and `estimationScaleId`; send the current scale id to keep it.
- `PokerSessions_UpdateRoundLabel` — replaces the label; omitting it clears it.
- `PokerSessions_RemoveRound` — deletes a round with its votes and consensus.
- `PokerSessions_DeleteSession` — deletes the session with every round, vote and estimate, whatever its status. To keep the results, complete it instead.

Write tools take UUIDs: the session's `id` and the round's `id` from `PokerSessions_GetSession`.

### Managing estimation scales

| Goal | Tool | Notes |
| --- | --- | --- |
| Create | `EstimationScales_CreateScale` | `name`, optional `description`, `values` in display order. Starts active. Returns the integer id. |
| Change | `EstimationScales_UpdateScale` | **Whole-record overwrite** of `name`, `description` and `values`; the body repeats the id as `estimationScaleId`. |
| Retire or bring back | `EstimationScales_SetActiveStatus` | Body `{ "id": <id>, "isActive": false }`. Fails if the scale already has that status, or if it is the last active scale. |
| Delete | `EstimationScales_DeleteScale` | Refused while any session uses the scale, or for the last active scale. |

Before `EstimationScales_UpdateScale`, read the scale and send back every value that should stay. **Changing `values` changes every session on that scale, past ones included** — a completed session's recorded votes and consensus estimates stay as they were, but may no longer appear among its scale's cards. When a scale is in use, suggest creating a new scale instead and deactivating the old one.

Prefer deactivating to deleting: a scale any session has used cannot be deleted at all.
