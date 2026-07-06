import { useEffect, useRef, type MutableRefObject } from 'react'
import type {
  ColumnOrderState,
  ColumnPinningState,
  ColumnSizingState,
  VisibilityState,
} from '@tanstack/react-table'

import type { GridState } from './use-grid-table'

/**
 * Opt-in localStorage persistence of a grid's user column layout.
 *
 * Persists exactly four {@link GridState} slices — `columnSizing`,
 * `userColumnVisibility` (the raw user layer, never the merged
 * columnVisibility), `columnPinning`, and `columnOrder` — under
 * `wayd-grid:{persistStateKey}:v{GRID_STATE_VERSION}`. Sorting, filters, and
 * search are deliberately session-only.
 *
 * Design notes:
 * - State is applied in a mount effect (one post-mount re-render), NOT via a
 *   lazy useState initializer: WaydGrid is SSR-rendered by most consumers and
 *   column widths appear in the markup, so reading localStorage during the
 *   first render would cause a hydration mismatch.
 * - A grid whose layout matches the defaults has NO stored entry (the entry
 *   is removed rather than written), so Reset Columns doubles as "delete the
 *   stored layout".
 * - Cross-tab sync is deliberately omitted: last writer wins and the next
 *   mount picks it up. Concurrent same-key grids are safe — the whole payload
 *   is written atomically.
 * - Persisted ids for columns absent from the current defs are inert:
 *   TanStack ignores unknown ids in sizing/visibility maps and filters them
 *   from pinning arrays. `columnOrder` is reconciled against the live defs by
 *   the grid (reconcileColumnOrder) before it reaches the table, so a stale or
 *   partial order can't strand columns. Bump {@link GRID_STATE_VERSION} on
 *   wholesale shape or column renames.
 * - `columnOrder` is optional in the persisted payload: v1 entries written
 *   before column ordering existed have no `columnOrder` key and still load
 *   (treated as no custom order), so the version stays at 1.
 */

/** Prefix shared by every key this feature writes to localStorage. */
export const GRID_STATE_KEY_PREFIX = 'wayd-grid:'

/** Bump when the persisted payload shape changes — stale versions are removed on load. */
export const GRID_STATE_VERSION = 1

/**
 * Device-wide kill switch (Account → Preferences). Absent = enabled. Stored
 * layouts are kept while disabled so re-enabling restores them.
 */
export const GRID_PERSISTENCE_ENABLED_KEY = `${GRID_STATE_KEY_PREFIX}persistence-enabled`

const SAVE_DEBOUNCE_MS = 250

export interface PersistedColumnState {
  columnSizing: ColumnSizingState
  userColumnVisibility: VisibilityState
  columnPinning: ColumnPinningState
  /** Optional so pre-ordering v1 entries stay valid; absent = no custom order. */
  columnOrder?: ColumnOrderState
}

/** The versioned localStorage key for a grid's persisted column state. */
export const gridStateStorageKey = (persistStateKey: string): string =>
  `${GRID_STATE_KEY_PREFIX}${persistStateKey}:v${GRID_STATE_VERSION}`

/**
 * Whether column layout persistence is enabled on this device. Read at
 * effect time by the grid hook (grids remount on navigation, so a toggle on
 * the Account page takes effect on the next grid mount).
 */
export function isGridPersistenceEnabled(): boolean {
  if (typeof window === 'undefined') return false
  try {
    return (
      window.localStorage.getItem(GRID_PERSISTENCE_ENABLED_KEY) !== 'false'
    )
  } catch {
    return false
  }
}

/**
 * Removes every persisted grid layout (all `wayd-grid:*` entries except the
 * enabled flag). The Account page's "Reset all grid layouts" action.
 */
export function clearAllGridColumnState(): void {
  if (typeof window === 'undefined') return
  try {
    const keysToRemove: string[] = []
    for (let i = 0; i < window.localStorage.length; i++) {
      const key = window.localStorage.key(i)
      if (
        key?.startsWith(GRID_STATE_KEY_PREFIX) &&
        key !== GRID_PERSISTENCE_ENABLED_KEY
      ) {
        keysToRemove.push(key)
      }
    }
    keysToRemove.forEach((key) => window.localStorage.removeItem(key))
  } catch (error) {
    console.error('Error clearing persisted grid column state:', error)
  }
}

const isPlainObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const isRecordOf = (
  value: unknown,
  entryType: 'number' | 'boolean',
): boolean =>
  isPlainObject(value) &&
  Object.values(value).every((entry) => typeof entry === entryType)

/** A string[] (columnPinning side, columnOrder) or absent. */
const isStringArrayOrAbsent = (value: unknown): boolean =>
  value === undefined ||
  (Array.isArray(value) && value.every((id) => typeof id === 'string'))

/** Shape guard for a parsed payload — malformed entries are ignored on load. */
export function isPersistedColumnState(
  value: unknown,
): value is PersistedColumnState {
  if (!isPlainObject(value)) return false
  const { columnSizing, userColumnVisibility, columnPinning, columnOrder } =
    value
  return (
    isRecordOf(columnSizing, 'number') &&
    isRecordOf(userColumnVisibility, 'boolean') &&
    isPlainObject(columnPinning) &&
    isStringArrayOrAbsent(columnPinning.left) &&
    isStringArrayOrAbsent(columnPinning.right) &&
    isStringArrayOrAbsent(columnOrder)
  )
}

/**
 * Serializes the three slices, or null when everything is default — the
 * null payload means "remove the entry" so pristine/reset grids leave no
 * localStorage residue.
 */
function buildPayloadJson(
  columnSizing: ColumnSizingState,
  userColumnVisibility: VisibilityState,
  columnPinning: ColumnPinningState,
  columnOrder: ColumnOrderState,
): string | null {
  const isDefault =
    Object.keys(columnSizing).length === 0 &&
    Object.keys(userColumnVisibility).length === 0 &&
    !columnPinning.left?.length &&
    !columnPinning.right?.length &&
    columnOrder.length === 0
  if (isDefault) return null
  const payload: PersistedColumnState = {
    columnSizing,
    userColumnVisibility,
    columnPinning,
    // Omit an empty order so the payload matches a grid that never reordered
    // (keeps entries minimal; the load path defaults a missing order to []).
    ...(columnOrder.length > 0 ? { columnOrder } : {}),
  }
  return JSON.stringify(payload)
}

/** Removes older-versioned (and unversioned) entries for the same grid. */
function removeStaleVersions(persistStateKey: string, storageKey: string) {
  const unversionedKey = `${GRID_STATE_KEY_PREFIX}${persistStateKey}`
  const versionPrefix = `${unversionedKey}:v`
  const keysToRemove: string[] = []
  for (let i = 0; i < window.localStorage.length; i++) {
    const key = window.localStorage.key(i)
    if (
      key !== null &&
      key !== storageKey &&
      (key === unversionedKey || key.startsWith(versionPrefix))
    ) {
      keysToRemove.push(key)
    }
  }
  keysToRemove.forEach((key) => window.localStorage.removeItem(key))
}

const tryParseJson = (raw: string): unknown => {
  try {
    return JSON.parse(raw)
  } catch {
    return undefined
  }
}

interface PendingWrite {
  storageKey: string
  /** null = remove the entry (all-default layout). */
  json: string | null
}

function flushPendingWrite(
  pendingWriteRef: MutableRefObject<PendingWrite | null>,
  lastWrittenRef: MutableRefObject<string | null>,
) {
  const pending = pendingWriteRef.current
  if (!pending) return
  pendingWriteRef.current = null
  try {
    if (pending.json === null) {
      window.localStorage.removeItem(pending.storageKey)
    } else {
      window.localStorage.setItem(pending.storageKey, pending.json)
    }
    lastWrittenRef.current = pending.json
  } catch (error) {
    console.error(
      `Error writing localStorage key "${pending.storageKey}":`,
      error,
    )
  }
}

type PersistableGridState = Pick<
  GridState,
  | 'columnSizing'
  | 'setColumnSizing'
  | 'userColumnVisibility'
  | 'setUserColumnVisibility'
  | 'columnPinning'
  | 'setColumnPinning'
  | 'columnOrder'
  | 'setColumnOrder'
>

/**
 * Loads the grid's persisted column layout on mount and saves changes back
 * (debounced — column resize fires per mousemove). No-ops when
 * `persistStateKey` is undefined or persistence is disabled on this device.
 *
 * `persistStateKey` must be stable for the life of the grid.
 */
export function useGridColumnStatePersistence(
  gridState: PersistableGridState,
  persistStateKey: string | undefined,
): void {
  const {
    columnSizing,
    setColumnSizing,
    userColumnVisibility,
    setUserColumnVisibility,
    columnPinning,
    setColumnPinning,
    columnOrder,
    setColumnOrder,
  } = gridState

  const storageKey = persistStateKey
    ? gridStateStorageKey(persistStateKey)
    : undefined

  // Gates saving until the load effect ran — without it the save effect could
  // clobber the stored entry with defaults on mount. Stays false when
  // persistence is off, which also disables saving.
  const loadedRef = useRef(false)
  const lastWrittenRef = useRef<string | null>(null)
  const pendingWriteRef = useRef<PendingWrite | null>(null)

  // ─── Load once on mount ─────────────────────────────────
  useEffect(() => {
    if (!persistStateKey || !storageKey) return
    if (!isGridPersistenceEnabled()) return
    try {
      removeStaleVersions(persistStateKey, storageKey)
      const raw = window.localStorage.getItem(storageKey)
      if (raw) {
        const parsed = tryParseJson(raw)
        if (isPersistedColumnState(parsed)) {
          const loadedOrder = parsed.columnOrder ?? []
          setColumnSizing(parsed.columnSizing)
          setUserColumnVisibility(parsed.userColumnVisibility)
          setColumnPinning(parsed.columnPinning)
          setColumnOrder(loadedOrder)
          // Normalized so the post-apply save pass short-circuits.
          lastWrittenRef.current = buildPayloadJson(
            parsed.columnSizing,
            parsed.userColumnVisibility,
            parsed.columnPinning,
            loadedOrder,
          )
        } else {
          // Unusable entry (malformed JSON or wrong shape): discard it so the
          // grid self-heals instead of re-reporting it on every mount.
          console.error(
            `Discarding unusable persisted grid state under "${storageKey}"`,
          )
          window.localStorage.removeItem(storageKey)
        }
      }
    } catch (error) {
      console.error(`Error reading localStorage key "${storageKey}":`, error)
    }
    loadedRef.current = true
  }, [
    persistStateKey,
    storageKey,
    setColumnSizing,
    setUserColumnVisibility,
    setColumnPinning,
    setColumnOrder,
  ])

  // ─── Save on change (trailing debounce) ─────────────────
  useEffect(() => {
    if (!storageKey || !loadedRef.current) return
    const json = buildPayloadJson(
      columnSizing,
      userColumnVisibility,
      columnPinning,
      columnOrder,
    )
    if (json === lastWrittenRef.current) {
      // Values are back to what's stored — cancel any pending stale write.
      pendingWriteRef.current = null
      return
    }
    pendingWriteRef.current = { storageKey, json }
    const timer = setTimeout(
      () => flushPendingWrite(pendingWriteRef, lastWrittenRef),
      SAVE_DEBOUNCE_MS,
    )
    return () => clearTimeout(timer)
  }, [
    storageKey,
    columnSizing,
    userColumnVisibility,
    columnPinning,
    columnOrder,
  ])

  // ─── Flush a pending write on unmount ───────────────────
  // Runs after the save effect's cleanup (declaration order), so the cleared
  // timer's write still lands instead of being lost on navigation.
  useEffect(
    () => () => flushPendingWrite(pendingWriteRef, lastWrittenRef),
    [],
  )
}
