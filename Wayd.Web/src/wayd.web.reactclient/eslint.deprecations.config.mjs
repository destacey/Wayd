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
]

export default eslintDeprecationsConfig
