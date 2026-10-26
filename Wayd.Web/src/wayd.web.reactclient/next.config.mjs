import { withSerwist } from '@serwist/turbopack'

/**
 * @type {import('next').NextConfig}
 */
const nextConfig = {
  output: 'standalone',
  reactCompiler: true,
  // Loaded from node_modules rather than bundled: the auto-instrumentations pull in
  // systeminformation (via the host-metrics instrumentation), whose platform-specific
  // binary lookups Turbopack cannot resolve, and instrumentations patch modules by
  // hooking require, which only works on unbundled packages.
  serverExternalPackages: [
    '@opentelemetry/api',
    '@opentelemetry/sdk-node',
    '@opentelemetry/sdk-trace',
    '@opentelemetry/auto-instrumentations-node',
  ],
  // @opentelemetry/instrumentation-http loads forwarded-parse through a
  // createRequire wrapper that output tracing cannot follow, so the standalone
  // build leaves it out and the server crashes loading instrumentation.
  outputFileTracingIncludes: {
    '/*': ['./node_modules/forwarded-parse/**/*'],
  },
  experimental: {
    // Puts the server span's traceparent into the document so browser API
    // calls can continue that trace. Client navigations use the traceresponse
    // header instead; the meta tag is the full page load.
    clientTraceMetadata: ['traceparent', 'tracestate'],
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
  // The consent page approves access to Wayd for another application, so no
  // other site may frame it and overlay its Approve button. Only this route:
  // silent sign-in loads /login in a same-origin iframe.
  async headers() {
    return [
      {
        source: '/oauth/consent',
        headers: [
          { key: 'Content-Security-Policy', value: "frame-ancestors 'none'" },
          { key: 'X-Frame-Options', value: 'DENY' },
        ],
      },
    ]
  },
  // Sprint URLs from before the pages moved under /work, still in bookmarks and shared links.
  async redirects() {
    return [
      {
        source: '/planning/sprints',
        destination: '/work/sprints',
        permanent: true,
      },
      {
        source: '/planning/sprints/:key',
        destination: '/work/sprints/:key',
        permanent: true,
      },
      // The in-app docs page for the report once named the Allocation Report.
      {
        source: '/docs/user-guide/work-management/allocation-report',
        destination:
          '/docs/user-guide/work-management/work-distribution-report',
        permanent: true,
      },
    ]
  },
}

export default withSerwist(nextConfig)
