import { AsyncLocalStorage } from 'node:async_hooks'

// Next captures `globalThis.AsyncLocalStorage` once, at the top level of
// next/dist/server/app-render/async-local-storage.js. If that module loads before the global is set,
// it caches a fake whose every method throws "AsyncLocalStorage accessed in runtime where it is not
// available" — and no later assignment can undo it.
//
// jsdom does not provide the class, so it has to come from node:async_hooks here in setupFiles,
// before any module loads. Setting it in setupFilesAfterEnv is a race: it happens to win whenever
// an earlier suite in the same worker has already pulled Next in, which is why a suite that passes
// in a full run can fail when run on its own.
if (typeof (globalThis as { AsyncLocalStorage?: unknown }).AsyncLocalStorage === 'undefined') {
  ;(globalThis as { AsyncLocalStorage?: unknown }).AsyncLocalStorage =
    AsyncLocalStorage
}
