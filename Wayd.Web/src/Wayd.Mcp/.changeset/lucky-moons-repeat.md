---
'@wayd/mcp': minor
---

Add project scoring and portfolio ranking read tools.

- `Projects_GetScoringContext` — the scoring model assigned to a project's portfolio, whether that model is archived, and the project's current score.
- `Projects_GetScores` — a project's full scoring history, headline values only.
- `Projects_GetScore` — one score in full, including every criterion rating and computed output in the frozen snapshot recorded at scoring time.
- `Portfolios_GetRankingScoreboard` — the score breakdown behind a portfolio's ranking board: the model definition plus per-project ratings and outputs.

All four are read-only and take UUIDs rather than keys. Recording a score and reordering ranks remain unavailable through MCP.

Two behaviours worth knowing, both documented in the `wayd-ppm` skill: a project appears in the scoreboard with empty ratings and outputs when it is unscored **or** when its latest score used a different or older model than the portfolio's current one, so empty does not mean "scored zero"; and the scoreboard returns breakdowns keyed by project ID without names or positions, so it needs joining against the portfolio's project list to be readable.
