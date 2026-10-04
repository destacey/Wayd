# Project scoring and portfolio ranking

Scoring is **read-only through MCP**: no tool records a score or reorders a ranking.

A **scoring model** is assigned to a portfolio, and every project in that portfolio is scored against its criteria. A **score** is a frozen snapshot of the criterion ratings and computed outputs at scoring time. Re-scoring adds a new entry to the project's history, so an old score reflects the model as it was then, not as it is now.

## Tools

All four take **UUIDs only**, not project or portfolio keys.

| Goal                                                           | Tool                              |
| -------------------------------------------------------------- | --------------------------------- |
| A project's model, current score, and whether it can be scored | `Projects_GetScoringContext`      |
| A project's scoring history (headline values only)             | `Projects_GetScores`              |
| One score in full, every criterion rating and output           | `Projects_GetScore`               |
| Score breakdown across a portfolio's ranking board             | `Portfolios_GetRankingScoreboard` |

For the headline number alone, read `currentScore` on `Projects_GetProject` or `Projects_GetProjects` instead of calling a scoring tool.

## Traps

- **A null `scoringModel` from `Projects_GetScoringContext` means the portfolio has no model assigned**, so the project cannot be scored at all.
- **Empty `ratings` and `outputs` on the scoreboard do not mean "scored zero".** The project is either unscored or was last scored under a different or older model than the portfolio's current one.
- **The scoreboard has no names or positions** — only project ids. Join it against `Portfolios_GetPortfolioProjects` to label rows.
- **Never show `rank` to a user.** On project DTOs it is an opaque fractional sort key. The 1-based display position is `position`, populated only when results are scoped to a single portfolio.
