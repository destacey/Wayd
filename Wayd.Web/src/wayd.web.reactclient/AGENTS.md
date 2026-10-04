<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

## Wayd client

The root [AGENTS.md](../../../AGENTS.md) covers the client's conventions (API calls, theming, dates, state). These are the traps that cost real time here; none of them fail a build or a jsdom test.

### React Compiler and TanStack Table

- `next.config.mjs` sets `reactCompiler: true`. Do not add `useMemo`, `useCallback` or `React.memo` to compiled code — only to a component that opts out with `'use no memo'`, where nothing else memoizes.
- Compiler memoization is safe for grid code on TanStack Table v9: it issues a new `table` (and new objects for rows that changed) on each state change. The exception is a component that reads sort or pin state off `header.column` — v9 reuses that object across state changes, so the compiled cache freezes `getIsSorted()` / `getIsPinned()` at their first value and the arrow or checkmark never moves while the rows still sort. Such a component starts its body with `'use no memo'` (`GridHeaderCell`, `ColumnMenuTrigger`). The lint rule calling that directive unused is wrong there.

### Components

- **`Wayd*` marks a house component**, whether it wraps a library or is fully custom (`WaydDateRange` wraps nothing). Reach for one before the library it wraps, and keep the prefix when a component stops wrapping anything.
- **Never import antd `List`** — antd 6 deprecated it; use `WaydList` (`src/components/common/wayd-list.tsx`), the single place to swap when its replacement ships. `Form.List` is a different component and is fine.
- **WaydGrid must be a block-level child.** As a flex item of a container with no defined height (`<Flex vertical>` inside a card body), it collapses to its toolbar: the virtualizer sees a 0px viewport and renders no rows while the toolbar still counts them. An explicit `height` does not help. Mirror `src/app/account/profile/claims-grid.tsx`.
- **A controlled `Popover`/`Dropdown` flickers when its trigger DOM node is replaced** between renders — rc-trigger reads the remount as a click outside, whatever `open` says. Keep one stable trigger element and swap only its children; it shows up only when a state boundary is crossed.
- **Charts in a record page need `autoFit: true` and `useChartRemountOnResize()`** (`@/src/hooks`). G2 re-fits only on a window resize, so opening the facts rail or collapsing the sider never reaches it. Attach the hook's ref to a wrapper and pass `renderKey` as the chart's `key`.
- **Sort user-facing strings with `caseInsensitiveCompare`** (from the `@/src/components/common/wayd-grid` barrel), never a plain `.sort()`, which puts every capitalized value first.

### API client

- **Never set `transformResponse: (data) => data`** on the Axios instances in `src/services/clients.ts`. The NSwag client (14.7+) assigns `response.data` directly, so disabling Axios's JSON parsing makes every response a string.
- **A polymorphic request body puts `$type` first** — `{ $type: 'entra', ...values }`, never `{ ...values, $type }`. System.Text.Json reads the discriminator only from the first property and otherwise fails with "must specify a type discriminator" (a 500).

### Tooling

- **A server-side 404 on a route that exists, with no API call made, is a corrupted `.next/dev`** (truncated `routes.d.ts`), typically after adding a folder under a dynamic segment while `next dev` runs. Restart the dev server (optionally deleting `.next`) before looking at code; deleting the types alone is not enough.
- **Run Prettier on an explicit file list, never a glob.** `.prettierrc.json` sets no `printWidth`, so the 80-column default reflows files you never touched.
