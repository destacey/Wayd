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
  // The unified/remark/rehype/micromark ecosystem is pure ESM (~100 packages once transitive deps
  // are included), so these are matched by family rather than listed individually.
  //
  // Use only [a-z0-9-] classes here, never \w: next/jest rewrites `/` in these patterns to the
  // platform separator and mangles backslash escapes while doing it, turning `[\w-]` into a class
  // that matches a literal backslash. Appending is also not enough on its own — see the
  // transformIgnorePatterns rewrite below.
  // @tanstack/react-table v9 and its table-core dependency are ESM-only
  // ("type": "module", no CommonJS build — v8 shipped both), so they must be
  // transformed like the unified family below.
  transformIgnorePatterns: [
    'node_modules/(?!(@tanstack/[a-z0-9-]+|@ungap/structured-clone|bail|ccount|character-entities[a-z0-9-]*|character-reference-invalid|comma-separated-tokens|decode-named-character-reference|devlop|estree-util-[a-z0-9-]+|hast[a-z0-9-]*|html-url-attributes|html-void-elements|is-(alphabetical|alphanumerical|decimal|hexadecimal|plain-obj)|longest-streak|markdown-table|mdast[a-z0-9-]*|micromark[a-z0-9-]*|parse-entities|property-information|react-markdown|rehype[a-z0-9-]*|remark[a-z0-9-]*|space-separated-tokens|stringify-entities|trim-lines|trough|unified|unist[a-z0-9-]*|vfile[a-z0-9-]*|web-namespaces|zwitch)/)',
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
const resolveJestConfig = async (): Promise<Config> => {
  const resolved = await createJestConfig(config)()

  // next/jest prepends a blanket `node_modules` ignore that allows through only its own packages,
  // and Jest skips a file if ANY pattern matches — so the entry above can never take effect while
  // that one is present ("Custom config can append to transformIgnorePatterns but not modify it",
  // next/dist/build/jest/jest.js). Drop only that blanket pattern and keep the rest (.pnpm, CSS
  // modules). Without this, importing a pure-ESM package such as rehype-sanitize fails every suite
  // in the module graph with "SyntaxError: Unexpected token 'export'".
  resolved.transformIgnorePatterns = resolved.transformIgnorePatterns?.filter(
    (pattern) => !/^\W*node_modules\W+\(\?!\.pnpm\)/.test(pattern),
  )

  return resolved
}

export default resolveJestConfig
