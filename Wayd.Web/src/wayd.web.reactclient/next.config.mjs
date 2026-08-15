import { withSerwist } from '@serwist/turbopack'

/**
 * @type {import('next').NextConfig}
 */
const nextConfig = {
  output: 'standalone',
  reactCompiler: true,
  experimental: {
    optimizePackageImports: ['antd', '@ant-design/icons', '@ant-design/charts'],
    // typescript-eslint has no TS 7 support yet (typescript-eslint#10940), so
    // `typescript` is aliased to @typescript/typescript6 and the real TS 7
    // compiler is installed as @typescript/native (see package.json).
    //
    // Next's default CLI type-check shells out to `bin.tsc` of whatever the
    // `typescript` package resolves to, and the alias publishes `bin.tsc6`
    // instead — so that lookup fails. Using the JS compiler API keeps Next's
    // build-time type checking enabled, since the alias does provide it.
    useTypeScriptCli: false,
  },
}

export default withSerwist(nextConfig)
