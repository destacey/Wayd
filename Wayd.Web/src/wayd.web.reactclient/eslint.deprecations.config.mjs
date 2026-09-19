import base from './eslint.config.mjs'

const eslintDeprecationsConfig = [
  ...base,
  {
    files: ['**/*.ts', '**/*.tsx'],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: { '@typescript-eslint/no-deprecated': 'warn' },
  },
  {
    // Excluded from tsconfig (it compiles against the webworker lib), so the
    // project service has no type information for it.
    ignores: ['src/app/sw.ts'],
  },
]

export default eslintDeprecationsConfig
