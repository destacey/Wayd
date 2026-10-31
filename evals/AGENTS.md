# AGENTS.md — Skill Evaluation

Measures whether the skills in `skills/` make agents use the Wayd MCP tools correctly. The root [AGENTS.md](../AGENTS.md) applies here too; [README.md](README.md) explains how a run works and how to write a scenario.

```bash
npm install       # Install dependencies
npm run typecheck # Type-check
npm run lint      # Run linter
npm test          # Check the grader and that every scenario is well-formed
```

- **Never run the evaluation unprompted** (`npm run evals`): it runs Claude Code under the user's login and spends their plan's usage. `npm test` is free and covers what CI checks.
- **A skill change is measured before and after**, on the same day and model, as a new `v<N>` variant; a new rule in a skill deserves a scenario. A skill with scenarios has at least three.
- **The stub advertises the hosted server's catalogue** (`catalog.json`, fetched from a running API with `npm run catalog`, not committed). Whether a tool a scenario names exists is checked by `McpToolCatalogTests` in `Wayd.Web.Api.IntegrationTests`, not here.
- **Changing `harness.ts`, `scenario.ts`, `stub-server.ts` or `run-eval.mjs`** makes the runner refuse each results directory until `--approve-harness` records the new hash; say so when you change one.
