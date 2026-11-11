import nextCoreWebVitals from 'eslint-config-next/core-web-vitals'
import nextTypescript from 'eslint-config-next/typescript'
import reactCompiler from 'eslint-plugin-react-compiler'
import { FlatCompat } from '@eslint/eslintrc'

const compat = new FlatCompat({
  // import.meta.dirname is available after Node.js v20.11.0
  baseDirectory: import.meta.dirname,
})

// Library components that have a house replacement. See docs/contributing/design-system.mdx.
const restrictedAntdTable = {
  name: 'antd',
  importNames: ['Table'],
  message:
    'Use WaydGrid (@/src/components/common/wayd-grid) — variant="simple" for a small table in a section or form. See docs/contributing/wayd-grid.mdx.',
}
const restrictedAntdTableTypes = {
  group: ['antd/es/table', 'antd/lib/table'],
  message:
    'Type the columns with ColumnDef from @/src/components/common/wayd-grid-core and render them in WaydGrid.',
}
const restrictedAntdList = {
  name: 'antd',
  importNames: ['List'],
  message:
    'antd 6 deprecated List. Use WaydList (@/src/components/common/wayd-list). Form.List is unaffected.',
}

// antd hands form-wide settings (Form `disabled` and `size`, ConfigProvider) to its components through
// React contexts. A deep path can load a second copy of those contexts, and a component imported that
// way ignores the form around it. A type has no runtime module, so `import type` stays allowed.
const restrictedAntdDeepImports = {
  regex: '^antd/(es|lib)(/|$)',
  allowTypeImports: true,
  message:
    "Import the component from 'antd' (const { TextArea } = Input). A deep path can load a second copy of antd's contexts, so the component ignores <Form disabled>, size and ConfigProvider. Only `import type` may use a deep path.",
}

/**
 * Tables written before the WaydGrid rule was enforced. Migrate a file to WaydGrid and remove it here;
 * never add one.
 */
const legacyAntdTableFiles = [
  'src/app/demand-management/requests/[[]key]/_components/intake-request-score.tsx',
  'src/app/organizations/_components/capacity-plan/capacity-plan-grid.tsx',
  'src/app/organizations/_components/team-capacity/initialize-allocations-form.tsx',
  'src/app/planning/planning-intervals/[[]key]/_components/planning-interval-capacity-section.tsx',
  'src/app/planning/planning-intervals/_components/planning-interval-team-sprint-mappings.tsx',
  'src/app/ppm/projects/_components/change-project-lifecycle-form.tsx',
  'src/app/ppm/projects/_components/scoring/project-score-detail.tsx',
  'src/app/ppm/strategic-initiatives/_components/strategic-initiative-kpi-details-drawer.tsx',
  'src/app/settings/auth/providers/_components/stage-tenant-migration-form.tsx',
  'src/app/settings/background-jobs/_components/job-details-drawer.tsx',
  'src/app/settings/connections/[[]id]/_components/sync-history-tab.tsx',
  'src/app/settings/connections/[[]id]/azure-devops/projects.tsx',
  'src/app/settings/status-workflows/_components/status-remap-table.tsx',
  'src/app/settings/status-workflows/_components/workflow-transitions-editor.tsx',
  'src/components/common/import/import-columns-table.tsx',
  'src/components/common/work/backlog-health/backlog-health-report.tsx',
  'src/components/common/work/cadence-metrics/team-cadence-metrics-report.tsx',
  'src/components/common/work/sprint-metrics/team-sprint-metrics-report.tsx',
  'src/components/common/work/work-distribution/work-distribution-breakdown.tsx',
  'src/components/common/work/work-distribution/work-distribution-over-time.tsx',
  'src/components/common/work/work-distribution/work-distribution-team-matrix.tsx',
]

const eslintConfig = [
  ...nextCoreWebVitals,
  ...nextTypescript,
  ...compat.config({
    extends: ['prettier'],
    plugins: ['unused-imports'],

    rules: {
      'unused-imports/no-unused-imports': 'warn',
      '@typescript-eslint/no-explicit-any': 'off',
      '@typescript-eslint/no-unused-vars': 'off',
      '@typescript-eslint/no-unused-expressions': 'off',
      '@typescript-eslint/no-empty-object-type': 'off',
    },
  }),
  {
    plugins: { 'react-compiler': reactCompiler },
    rules: { 'react-compiler/react-compiler': 'warn' },
  },
  {
    // A later block replaces these options whole, so each one repeats every restriction it keeps.
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [restrictedAntdTable, restrictedAntdList],
          patterns: [restrictedAntdTableTypes, restrictedAntdDeepImports],
        },
      ],
    },
  },
  {
    files: legacyAntdTableFiles,
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [restrictedAntdList],
          patterns: [restrictedAntdDeepImports],
        },
      ],
    },
  },
  {
    // The wrapper itself is the one place that imports antd's List.
    files: ['src/components/common/wayd-list.tsx'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [restrictedAntdTable],
          patterns: [restrictedAntdTableTypes, restrictedAntdDeepImports],
        },
      ],
    },
  },
  {
    // incompatible with React Compiler (reads refs during render)
    files: ['src/app/service-unavailable/diagnostic-canvas.tsx'],
    linterOptions: { reportUnusedDisableDirectives: 'off' },
  },
  {
    ignores: ['coverage/**', 'src/services/wayd-api.ts'],
  },
]

export default eslintConfig
