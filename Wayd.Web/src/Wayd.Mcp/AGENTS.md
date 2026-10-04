# AGENTS.md — Wayd MCP Server

Publishes `@wayd/mcp`, exposing the Wayd API as agent tools. The repository-wide guidance in the root [AGENTS.md](../../../AGENTS.md) applies here too; this file adds what is specific to the package.

```bash
npm install       # Install dependencies
npm run build     # Regenerate Zod schemas, then compile
npm run typecheck # Type-check src, scripts, and tests
npm run lint      # Run linter
npm test          # Build, then run the test suite
```

Tool definitions live one file per area under `src/tools/`, registered in the `src/tools/index.ts` barrel. Agent-facing usage guidance lives in `skills/` at the repo root (`skills/wayd-ppm/SKILL.md` is the largest).

- **Adding a tool**: add the definition, register it in the barrel, then run `npm run build` — `scripts/generate-zod-schemas.ts` emits a Zod schema per `inputSchema`, and validation silently falls back to a permissive schema if you skip it. Update both `README.md` (capability tables) and the matching `skills/*/SKILL.md`.
- **Tool annotations gate confirmation**: `destructiveHint` tells clients to confirm before running. Every status transition, update, and delete carries it. `annotationsFor` (`src/annotations.ts`) defaults each tool from its HTTP method — only GET is read-only, every write starts destructive — then applies the definition's own `annotations`, so a write is advertised as safe only by a deliberate override — and only one that purely adds a record (`destructiveHint: false`; a tag that can replace another does not qualify). Every tool is `openWorldHint: false`, and `annotations.title` is required by the type. `tests/annotations.test.ts` and `tests/stdio-protocol.test.ts` assert this over the definitions and over the wire.
- **Array query parameters** need `paramsSerializer: { indexes: null }` (set in `src/executor.ts`). Axios defaults to `status[]=1`, which ASP.NET's model binder ignores, so array filters silently become no-ops rather than erroring.
- **Updates are whole-record overwrites**, matching the API's `PUT` semantics — an omitted field is cleared. Role lists (`sponsorIds`/`ownerIds`/`managerIds`/`memberIds`) replace that role's membership, and an omitted **or empty** list removes everyone in it. Tool descriptions must say so; the skill explains why it matters (wiping Owners/Managers can leave a record nobody is authorized to manage).
- **Server instructions** (`src/instructions.ts`) reach clients that never load the skills. Keep them to rules that span every tool, and change them when one of those rules changes.
- Tests use Node's built-in runner against `build/`, so `npm test` builds first. Nothing hits the network.
- **Every shipped change needs a changeset** (`npx changeset` in the package; `--empty` for a deliberate no-release). Without one the change merges cleanly and is never published, so clients drift from the API. Shipped means `src/`, `scripts/`, `README.md`, `package.json`, or an API change to an import row class (the import formats are generated from the spec). CI and the `pre-push` hook enforce it — see [mcp-server.mdx](../../../docs/contributing/mcp-server.mdx).
