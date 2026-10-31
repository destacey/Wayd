# Skill evaluation

Measures whether the agent skills in [`skills/`](../skills/) make agents use the Wayd MCP tools correctly. Each scenario is a user request with the behaviour a correct run shows — which tools, with what arguments, in what order, and what is confirmed with the user first. Every scenario runs without skills (`baseline`) and with them, on each model, so an edit to a skill can be shown to help or hurt.

Runs never call the Anthropic API or change a Wayd instance: they go through Claude Code (`claude -p`) under your own login, so they count against your plan's usage, and every tool call is answered from fixtures. A running Wayd API is needed once, to fetch the tool list the stub advertises.

## How a run works

1. The runner makes an empty directory outside the repository, so no `CLAUDE.md`, settings or memory from this repo reaches the agent. For a skill variant it copies all of `skills/` into the directory's `.claude/skills/`, so the agent has to choose and load the right skill itself.
2. It starts `claude -p` there with only the stub server (`--strict-mcp-config`), only the `Skill` and `Read` built-in tools, permission for the `wayd` server's tools alone, and project settings only (`--setting-sources project`). The directory has no settings, so your plugins, personal skills, output style and hooks stay out, while skills in its `.claude/skills/` load. (`--restricted` would hide those skills too.) A run whose session starts with Wayd skills in `baseline`, or without them in a skill variant, fails as a harness error rather than scoring.
3. [`stub-server.ts`](stub-server.ts) advertises what the hosted server does, from the saved catalogue — names, titles, descriptions, input schemas, annotations, server instructions — and answers each call from the scenario's fixtures, worded as the hosted server words its answers. A call missing a required argument is refused before any fixture is read; a call no fixture covers gets a 404. `Skills_Get` serves the skills the run installed, in every variant, as the hosted server does.
4. Each scripted reply in the scenario continues the same session (`--resume`), and the stub logs every call with the number of replies the agent had received.
5. [`scenario.ts`](scenario.ts) grades the call log: `pass` is 1 when every assertion holds, `score` is the fraction that hold.

## Layout

| Path | What it holds |
| --- | --- |
| `scenarios/<skill>.json` | The scenarios for one skill; the schema and matcher rules are in `scenario.ts` |
| `scenario.ts` | Scenario types, the argument matcher, the grader, and structural validation |
| `catalog.ts` | Fetches the hosted server's tool list and instructions into `catalog.json` (`npm run catalog`), and loads it |
| `stub-server.ts` | The MCP server the agent talks to |
| `harness.ts` | Loads scenarios, runs one through `claude -p`, grades it |
| `run-eval.mjs` | The runner: concurrency, resume, per-case time limit, results files (from the claude-api skill's scaffold) |
| `build-report.mjs` | Builds `report.html` from a results directory (the claude-api skill's lite report builder) |
| `results/<model>/` | One directory per model: `_state.json` (metrics, columns, approved harness), then `baseline/` and `v<N>/` |

## Variants

Each results directory's `_state.json` says how each variant offers the skills (`variants.<id>.skills`):

| Mode | What the agent gets | What it measures |
| --- | --- | --- |
| `none` | No Wayd skills installed | The tools, their descriptions, the server instructions and `Skills_Get`, as a client that does not load skills sees them |
| `available` | Every skill installed; it must choose one itself, as in a user's Claude Code | Whether a skill is chosen, and whether it helps when it is |
| `preloaded` | The opening message invokes the scenario's skill (`/wayd-ppm …`), as a user can | What the skill's content does, regardless of selection |

`baseline` is `none`, `v1` is `available` and `v2` is `preloaded`, all with the skills at the commit where this evaluation was added. A variant can pin its skills to a commit with `ref` (`{ "skills": "preloaded", "ref": "27ed23359" }`), which installs `skills/` as it was there, so the version before a change can run beside the version after it on the same day. The `skill_loaded` column shows whether the scenario's skill was loaded. A skill variant runs with the skills as they are on disk, so a later skill change gets new variants (`v3`, `v4`, …), each added to `variants` with its mode and given a `change.md` whose first line names the change and its commit — that line is the description the report shows.

Compare a skill change against the latest `v<N>` on the same model, not against `baseline`, and run both on the same day: rerun the previous variant into a new `v<N>` rather than reuse old rows. Claude Code tells the model today's date while the fixtures' dates are fixed, so a scenario can read differently weeks later — a current planning interval becomes a past one, an expiry date passes — and scores drift with no skill change. If drift appears, the fix is date placeholders resolved at run time.

## Running

From `evals/`, with Claude Code installed and signed in. First fetch the tool list from a running API with the **MCP Server** feature flag on, using a personal access token whose account can see every tool; fetch it again when the API's tools change, and use the same one for every run you compare:

```bash
npm install
WAYD_API_KEY=<token> npm run catalog -- https://localhost:<api-port>/mcp

# First run in a results directory: review the harness files, then record their hash
npm run evals -- --flow results/claude-sonnet-5-5 --model claude-sonnet-5-5 --variant baseline --approve-harness

# Both conditions on one model
npm run evals -- --flow results/claude-sonnet-5-5 --model claude-sonnet-5-5 --variant baseline
npm run evals -- --flow results/claude-sonnet-5-5 --model claude-sonnet-5-5 --variant v1

# One skill or scenario only (comma-separated skill names or scenario ids)
WAYD_EVAL_ONLY=wayd-ppm npm run evals -- --flow results/claude-haiku-5-5 --model claude-haiku-5-5 --variant v1

# The report
node build-report.mjs results/claude-sonnet-5-5/
```

The runner refuses to start when `harness.ts`, `scenario.ts`, `stub-server.ts` or `run-eval.mjs` changed since the hash in that directory's `_state.json` was recorded; rerun with `--approve-harness` once you have reviewed the change. Rows are written as each case finishes, and a rerun skips cases already in `results.jsonl`, so an interrupted run resumes. `--reps N` runs each scenario N times; `--concurrency N` (default 4) sets how many `claude` processes run at once.

`results.jsonl` holds one row per scenario and repetition, with `explanation.pass` listing each failed assertion, plus the calls and final message it was graded on. After fixing an assertion, rescore the recorded runs instead of running them again: `npm run regrade -- results/<model>`. A fixture change still needs a new run, because fixtures shape what the agent does. `traces/` holds the transcripts and is not committed. The `api_equiv_usd` column is what Claude Code reports the run would have cost at API prices — a measure of size, not a charge on a subscription.

## Writing a scenario

- Write the prompt in a user's voice. Never name a tool or hint at the trap being probed.
- Cover every call a correct run makes with a fixture, including lookups and other reasonable ways to find the same record. A non-zero `unfixtured_calls` in the results means a scenario is missing one.
- Assert what a correct run must or must not do, and leave legitimate alternatives open. Prefer the arguments of the change itself over which read found the record. Give each assertion a `because`.
- A change that needs the user's say-so gets a prompt that states the intent without approving it, a reply that approves that change by name (`"Yes, rename it."`), and both `confirmedBefore` and `called` assertions. A bare "Yes" approves whatever the agent offered last — including an extra it volunteered, like deleting a record it had just retired.
- Use invented data: names and emails at `acme.example`, freshly made UUIDs.

`npm test` validates every scenario's structure and fails a scenario that an idle run would pass; `McpToolCatalogTests` in the API's integration tests fails a scenario that names a tool the server does not offer.

## Known limits

- Claude Code's built-in skills are available in every run, with and without the Wayd skills.
- Results depend on the Claude Code version as much as on the skills; each row records `claude_code_version`, so compare variants run on the same one. A Claude Code too old to recognize the model fails the run rather than score it — update Claude Code.
- Runs are non-interactive (`claude -p`), and Claude Code's prompt for that mode may make an agent less inclined to stop and ask than it would be in a chat. That affects both conditions alike.
- The stub checks only that required arguments are present. The hosted server's model binding, validation and the endpoint's own refusals do not run, so a call with a malformed argument gets the fixture a correct one would.
- The stub advertises no output schemas, so a client does not check fixtures against them, and answers with text only. The agent reads the same text either way.
- The local API's development certificate must be trusted by Node to fetch the catalogue over HTTPS; otherwise use the API's HTTP address.
