import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

const root = fileURLToPath(new URL('./', import.meta.url))

export default defineConfig({
  resolve: {
    // tsconfig's `@/*` path. Matches only `@/`, so scoped packages (`@ant-design/...`) are untouched.
    alias: [
      { find: /^@\//, replacement: root },
      // Both packages otherwise resolve to their CommonJS builds, which pre-bundling (below) reduces
      // to a default export alone; their ESM builds keep every component as a named export.
      { find: /^antd$/, replacement: `${root}node_modules/antd/es/index.js` },
      {
        find: /^@ant-design\/icons$/,
        replacement: `${root}node_modules/@ant-design/icons/es/index.js`,
      },
    ],
  },
  test: {
    // A fresh jsdom per test file is the largest fixed cost of a run, so only component suites
    // (.test.tsx) pay it. A .test.ts suite that renders a hook or touches the DOM opts in with a
    // `// @vitest-environment jsdom` first line.
    projects: [
      {
        extends: true,
        test: {
          name: 'dom',
          environment: 'jsdom',
          include: ['src/**/*.test.tsx'],
        },
      },
      {
        extends: true,
        test: {
          name: 'logic',
          environment: 'node',
          include: ['src/**/*.test.ts'],
        },
      },
    ],
    environmentOptions: {
      // jsdom's own default origin; some suites build or compare absolute URLs against it.
      jsdom: { url: 'http://localhost/' },
    },
    globals: true,
    // Suites select elements by CSS-module class (`tr.trActive`), so modules keep their source names.
    css: {
      include: [/\.module\.css$/],
      modules: { classNameStrategy: 'non-scoped' },
    },
    setupFiles: ['./src/vitest.setup.ts'],
    // Puts back every `vi.spyOn` before each test, so a spy on a global (console, storage, window)
    // never outlives the test that made it, even one that fails before its own cleanup.
    restoreMocks: true,
    // Every suite loads antd and its icons. Run natively, Node reads their CommonJS builds file by
    // file, afresh in each test file, which took most of the run; pre-bundled, each is one module.
    server: { deps: { inline: ['antd', '@ant-design/icons'] } },
    deps: {
      optimizer: {
        client: { enabled: true, include: ['antd', '@ant-design/icons'] },
        ssr: { enabled: true, include: ['antd', '@ant-design/icons'] },
      },
    },
    // A jsdom worker per core starves the editor running beside the tests, so a local run takes half
    // the cores; CI has the machine to itself.
    maxWorkers: process.env.CI ? undefined : '50%',
    // The slowest userEvent-driven suites take about 3s locally; this leaves room for a loaded CI
    // runner collecting coverage without letting a hung test sit for a minute.
    testTimeout: 30_000,
    coverage: {
      provider: 'v8',
      reporter: ['lcov', 'text-summary'],
      // Source only: the setup files and CSS modules would otherwise count towards the figure.
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/vitest.setup*.ts'],
    },
  },
})
