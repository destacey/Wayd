import { defineConfig } from 'vitest/config'
import baseConfig from './vitest.config.mts'

// The docs suite reads the MDX tree from disk, so it runs in node without the browser setup file.
export default defineConfig({
  resolve: baseConfig.resolve,
  test: {
    environment: 'node',
    globals: true,
    include: ['src/services/docs.test.ts'],
  },
})
