import type { Config } from 'jest'
import nextJest from 'next/jest.js'

const createJestConfig = nextJest({
  // Provide the path to your Next.js app to load next.config.js and .env files in your test environment
  dir: './',
})

// Add any custom config to be passed to Jest
const config: Config = {
  coverageProvider: 'v8',
  testEnvironment: 'jsdom',
  // Add more setup options before each test is run
  setupFilesAfterEnv: ['./src/jest.setup.ts'],
  // Add other ESM packages here if needed
  transformIgnorePatterns: [
    'node_modules/(?!(rehype-raw|react-markdown|remark-gfm|remark-parse|unified|mdast-util-to-string)/)',
  ],
  testPathIgnorePatterns: ['./.next/', './node_modules/'],
  moduleNameMapper: {
    // @ant-design/icons' CommonJS build hard-requires the ESM `es/generate`
    // entry of @ant-design/colors, which Jest can't parse. Redirect it to the
    // package's CommonJS `lib` build. See: @ant-design/icons >= 6.3.x.
    '^@ant-design/colors/es/(.*)$': '@ant-design/colors/lib/$1',
    '^@/(.*)$': '<rootDir>/$1',
  },
}

// createJestConfig is exported this way to ensure that next/jest can load the Next.js config which is async
export default createJestConfig(config)
