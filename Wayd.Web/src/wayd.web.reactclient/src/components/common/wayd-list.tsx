'use client'

import { List } from 'antd'
import type { ListProps } from 'antd'
import type { ListItemProps, ListItemMetaProps } from 'antd/es/list'

/**
 * WaydList — internal wrapper around Ant Design's `List`.
 *
 * Why this exists:
 * antd v6 soft-deprecated `List` (it logs `The \`List\` component is
 * deprecated...` on every render in dev — see node_modules/antd/es/list/
 * index.js). The component still works and will until v7, when antd plans to
 * replace it with `Listy` (rc-listy, not yet released).
 *
 * This wrapper does two things:
 *   1. Suppresses that one specific dev-only deprecation warning so it stops
 *      drowning out genuine console output.
 *   2. Gives us a single seam to swap to `Listy` when it ships — change the
 *      implementation here, not 11 call sites.
 *
 * Use exactly like antd's List: `<WaydList>`, `<WaydList.Item>`,
 * `<WaydList.Item.Meta>`. Do NOT import antd's `List` directly in feature code.
 *
 * NOTE: `Form.List` is a different, non-deprecated component — keep using that
 * directly from `Form`.
 */

const DEPRECATION_FRAGMENT = 'The `List` component is deprecated'
const SUPPRESSED_FLAG = '__waydListDeprecationSuppressed__'

/**
 * antd emits the deprecation via its internal `devUseWarning`, which routes to
 * `console.error`. We filter out only that exact message, once, leaving every
 * other warning intact.
 *
 * Only patches in development — in test we want console output untouched, and
 * in production the warning isn't emitted at all. The "already patched" flag
 * lives on `globalThis` so it survives Next.js Fast Refresh (HMR re-evaluates
 * this module, which would otherwise re-wrap `console.error` repeatedly).
 */
function suppressListDeprecationWarning() {
  if (process.env.NODE_ENV !== 'development') return

  const globalScope = globalThis as typeof globalThis & {
    [SUPPRESSED_FLAG]?: boolean
  }
  if (globalScope[SUPPRESSED_FLAG]) return
  globalScope[SUPPRESSED_FLAG] = true

  const originalError = console.error.bind(console)
  console.error = (...args: unknown[]) => {
    if (
      typeof args[0] === 'string' &&
      args[0].includes(DEPRECATION_FRAGMENT)
    ) {
      return
    }
    originalError(...args)
  }
}

suppressListDeprecationWarning()

const WaydList = List

export type { ListProps, ListItemProps, ListItemMetaProps }
export default WaydList
