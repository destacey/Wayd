/// <reference no-default-lib="true" />
/// <reference lib="esnext" />
/// <reference lib="webworker" />
import { defaultCache } from '@serwist/turbopack/worker'
import type {
  PrecacheEntry,
  RuntimeCaching,
  SerwistGlobalConfig,
} from 'serwist'
import { NetworkOnly, Serwist } from 'serwist'

declare global {
  interface WorkerGlobalScope extends SerwistGlobalConfig {
    __SW_MANIFEST: (PrecacheEntry | string)[] | undefined
  }
}

declare const self: ServiceWorkerGlobalScope

// Ahead of the defaults, which would cache a cross-origin response for an
// hour: the API is another origin, and its answers are one user's data. A
// request that carries credentials, or leaves this origin, is never cached.
const neverCached: RuntimeCaching = {
  matcher: ({ request, sameOrigin }) =>
    !sameOrigin || request.headers.has('Authorization'),
  handler: new NetworkOnly(),
}

const serwist = new Serwist({
  precacheEntries: self.__SW_MANIFEST,
  skipWaiting: true,
  clientsClaim: true,
  navigationPreload: true,
  runtimeCaching: [neverCached, ...defaultCache],
})

serwist.addEventListeners()
