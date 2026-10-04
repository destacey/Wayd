# React client tests

Jest with React Testing Library, in `Wayd.Web/src/wayd.web.reactclient`. The commands are in the Frontend Tests section of [docs/contributing/testing.mdx](../../../docs/contributing/testing.mdx). Run them from that directory — there is no `package.json` at the repository root.

## Writing the test

- Assert rendered output and behaviour, not implementation detail. Query by role or visible text; a class-name query breaks on refactors that change nothing a user sees.
- **WaydGrid and the other TanStack Table consumers carry a `'use no memo'` directive**, opting them out of React Compiler memoization. Manual `useMemo`/`useCallback` in those components is deliberate — do not remove it as a leftover.
- **Grid tests see a fixed window of rows, not the full data set.** jsdom has no layout, so the virtualizer would render nothing; the `data-grid-body-viewport` mock in `src/jest.setup.ts` gives the grid body a fixed size. Read that mock for the row count before asserting on rendered rows, and keep a test's data within the window unless it is testing virtualization.

## Running the mutation gate on a Jest suite

The mutation-gate loop applies unchanged; only the commands differ:

1. Baseline: `npm run test:ci`, and record the pass count.
2. For each behaviour, mutate the component or hook under test and run the narrowest suite: `npx jest <path>`.
3. Revert only the mutation, never the file.

The catalogue's Boundary, Logic, Guard, Return, and Collection rows apply. The Persistence and Status rows are .NET-specific.
