---
"@wayd/mcp": minor
---

Update dependencies and raise the minimum supported Node version to 22.

- **Node**: `engines.node` is now `>=22.0.0` (was `>=20.0.0`). Node 20 reaches end-of-life in April 2026; Node 22 and 24 remain supported.
- **Dependencies**: `@modelcontextprotocol/sdk` 1.30, `axios` 1.19, `dotenv` 17, `zod` 4.
- **Tooling**: ESLint 10, `@eslint/js` 10, `globals` 17, `@changesets/cli` 2.31, `@types/node` 24, `tsx` 4.23, `typescript-eslint` 8.67.

Fixes a stdout corruption bug surfaced by the dotenv 17 upgrade: dotenv now prints a startup banner to stdout by default, which broke the stdio transport's JSON-RPC stream. It is now loaded with `quiet: true`, keeping stdout reserved for the protocol.

Adds a test suite (`npm test`) covering the stdio transport, error formatting, and generated schema validation. It runs on Node's built-in test runner, so it adds no dependencies, and is wired into CI. The headline test asserts that stdout carries nothing but JSON-RPC — the failure that lint, typecheck, and build all missed above.

Tightens type checking with `noUncheckedIndexedAccess`, so dynamic key lookups yield `T | undefined`, and drops `noImplicitAny`/`strictNullChecks` from the compiler options since `strict` already implies both.

Moves the generated schemas onto Zod 4's top-level format validators (`z.uuid()`, `z.iso.date()`, `z.iso.datetime()`, `z.ZodType`) in place of the deprecated string-method forms. Each replacement was checked to validate identically to the form it replaces, and a test fails if the output ever drifts back onto a deprecated API.

Replaces the `json-schema-to-zod` dependency with a small in-house emitter (`scripts/json-schema-to-zod.ts`). The upstream project was archived in April 2026 and still emitted Zod 3 syntax. The replacement covers exactly the JSON Schema subset the API produces and throws on anything it does not recognise, so an unsupported keyword fails the build instead of silently weakening argument validation. It generates all 106 schemas byte-identically to the previous output.

Upgrades zod to 4, which renamed `ZodError.errors` to `.issues`. Argument validation read the removed property, so on zod 4 every validation failure would have thrown a `TypeError` instead of returning the "Invalid arguments" message that tells a model what to fix. Validation messages are unchanged in substance; zod's issue codes are more precise (for example `invalid_string` is now `invalid_format`).
