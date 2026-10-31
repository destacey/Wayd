# Project scoring and portfolio ranking

Every change here requires delivery leadership: an Owner or Manager of the project or an ancestor for a score, of the portfolio for its model and ranking.

A **scoring model** is assigned to a portfolio, and every project in that portfolio is scored against its criteria. A **score** is a frozen snapshot of the criterion ratings and computed outputs at scoring time. Re-scoring adds a new entry to the project's history, so an old score reflects the model as it was then, not as it is now.

## Reading scores

All four take **UUIDs only**, not project or portfolio keys.

| Goal                                                           | Tool                              |
| -------------------------------------------------------------- | --------------------------------- |
| A project's model, current score, and whether it can be scored | `Projects_GetScoringContext`      |
| A project's scoring history (headline values only)             | `Projects_GetScores`              |
| One score in full, every criterion rating and output           | `Projects_GetScore`               |
| Score breakdown across a portfolio's ranking board             | `Portfolios_GetRankingScoreboard` |

For the headline number alone, read `currentScore` on `Projects_GetProject` or `Projects_GetProjects` instead of calling a scoring tool.

## Recording a score

1. Call `Projects_GetScoringContext`. A null `scoringModel` means the portfolio has no model, so the project cannot be scored.
2. Gather a rating for **every** criterion in `scoringModel.criteria` — a partial score is rejected. A criterion with a scale is rated by choosing one of that scale's levels (`ratingLevelId`); any other takes a numeric `value`.
3. Show the user each criterion with the rating you will record, and confirm.
4. Call `Projects_RecordScore` with `ratings: [{ criterionId, ratingLevelId | value }]`. It returns the new score's id.

Each call adds a score to the history and makes it the current one; earlier scores stay. To correct a score, record a new one.

## A portfolio's scoring model

`Portfolios_AssignScoringModel` sets the model a portfolio's projects are scored against; it must be an **Active** scoring model, identified by its UUID. Find it with `ScoringModels_GetScoringModels` and `state: Active`, and use `ScoringModels_GetScoringModel` to show the user a model's criteria and formulas before choosing. It replaces any current model, and projects scored under the old one drop off the scoreboard until re-scored, so tell the user how many scored projects that affects. `Portfolios_ClearScoringModel` removes the model: recorded scores stay, but no project can be scored until one is assigned again. Neither works on an archived portfolio.

## Reordering the ranking

1. Read the board with `Portfolios_GetPortfolioProjects`; `position` is each project's 1-based place.
2. Build the move: `projectIds` — the projects to move, in their new order — and the anchors either side of where they land: `afterProjectId` (the project just above) and/or `beforeProjectId` (the project just below). For the top, give only `beforeProjectId` = the current first project; for the bottom, only `afterProjectId` = the current last. An anchor cannot be one of the moved projects.
3. Show the user the resulting order by name and position, and confirm.
4. Call `Portfolios_MoveProjectRanks`, then re-read the board to confirm.

`Portfolios_RebalanceProjectRanks` only tidies the underlying rank values; the order does not change, so never use it to reorder.

## Traps

- **A null `scoringModel` from `Projects_GetScoringContext` means the portfolio has no model assigned**, so the project cannot be scored at all.
- **A project missing from the scoreboard is not "scored zero".** It is either unscored or was last scored under a different or older model than the portfolio's current one; the scoreboard lists only projects whose current score uses the current model.
- **The scoreboard has no names or positions** — only project ids. Join it against `Portfolios_GetPortfolioProjects` to label rows.
- **Never show `rank` to a user.** On project DTOs it is an opaque fractional sort key. The 1-based display position is `position`, populated only when results are scoped to a single portfolio.
