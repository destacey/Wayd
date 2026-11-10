# React client tests

Vitest with React Testing Library, in `Wayd.Web/src/wayd.web.reactclient`. The commands are in the Frontend Tests section of [docs/contributing/testing.mdx](../../../docs/contributing/testing.mdx). Run them from that directory — there is no `package.json` at the repository root.

## Writing the test

- Assert rendered output and behaviour, not implementation detail. Query by role or visible text; a class-name query breaks on refactors that change nothing a user sees.
- **Only `GridHeaderCell` and `ColumnMenuTrigger` carry a `'use no memo'` directive** (they read sort and pin state off `header.column`, which TanStack Table v9 reuses). Manual `useMemo`/`useCallback` belongs only in components with that directive; elsewhere the React Compiler memoizes. See the client's `AGENTS.md`.
- **Never wrap a `userEvent` call in `act()`.** user-event already runs inside RTL's `act`, and switches React's act environment off between its events; an outer `act` keeps the act queue open across that gap, so any update landing there prints "The current testing environment is not configured to support act(...)" — only under load, which hides it locally. To wait for what an event causes, `await` a `findBy*` query or `waitFor`.
- **Grid tests see a fixed window of rows, not the full data set.** jsdom has no layout, so the virtualizer would render nothing; the `data-grid-body-viewport` mock in `src/vitest.setup.dom.ts` gives the grid body a fixed size. Read that mock for the row count before asserting on rendered rows, and keep a test's data within the window unless it is testing virtualization.

## Running the mutation gate on a Vitest suite

The mutation-gate loop applies unchanged; only the commands differ:

1. Baseline: `npx vitest run <path>` on the suite you will mutate, and record the pass count.
2. For each behaviour, mutate the component or hook under test and run the narrowest suite: `npx vitest run <path>`.
3. Revert only the mutation, never the file.

The catalogue's Boundary, Logic, Guard, Return, and Collection rows apply. The Persistence and Status rows are .NET-specific.
