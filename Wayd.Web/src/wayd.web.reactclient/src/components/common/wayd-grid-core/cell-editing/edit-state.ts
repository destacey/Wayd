import { toApiError } from '@/src/utils/problem-details'
import type { CellEditor, CellEditPhase } from './types'

/**
 * One cell's edit, from the moment the user leaves it until the server has
 * the value. Kept apart from the rows, so a refetch cannot overwrite it and
 * nothing is read back from what the DOM happens to have mounted.
 */
export interface CellEntry {
  /** The row's data id, as `onCommit` receives it. */
  rowKey: string
  /** The patch key the cell's editor writes. */
  field: string
  /** The column the cell is in, where its mark shows. */
  columnId: string
  /** Held, in flight, confirmed or failed. */
  phase: CellEditPhase
  /** The user's value, in model form. Absent on a cell marked only by a
   *  server error about a field the user did not change. */
  value?: unknown
  /** Whether `value` holds the user's value (an `undefined` one included). */
  hasValue: boolean
  /** The message shown on the cell, prefixed with the column label. */
  error?: string
  /** Bumped on every write, so a late server reply cannot overwrite a newer
   *  edit of the same cell. */
  seq: number
  /** Data version at which a commit succeeded (phase `settled`). */
  settledAt?: number
  /** How the value is shown on its row before the server has it. */
  apply?: CellEditor['apply']
}

export type CellEntries = Readonly<Record<string, CellEntry>>

/**
 * The id of the element describing a marked cell. Row and column ids may
 * hold characters an `aria-describedby` id list cannot, so they are folded
 * to safe ones.
 */
export const cellMessageId = (
  prefix: string,
  rowKey: string,
  columnId: string,
) => `${prefix}-${`${rowKey}-${columnId}`.replace(/[^A-Za-z0-9_-]/g, '_')}-msg`

export const cellKey = (rowKey: string, field: string) =>
  `${rowKey}\u0000${field}`

/** The patch key an editor writes for a row. */
export const editorField = <T>(editor: CellEditor<T>, row: T): string =>
  typeof editor.field === 'function' ? editor.field(row) : editor.field

/** `Object.is`, or element-wise for arrays (an assignee list). */
export const defaultEquals = (a: unknown, b: unknown): boolean => {
  if (Array.isArray(a) && Array.isArray(b)) {
    return a.length === b.length && a.every((v, i) => Object.is(v, b[i]))
  }
  // An empty value is empty however the server and the editor spell it.
  if (
    (a === null || a === undefined || a === '') &&
    (b === null || b === undefined || b === '')
  ) {
    return true
  }
  return Object.is(a, b)
}

export const editorEquals = <T>(
  editor: CellEditor<T>,
  a: unknown,
  b: unknown,
) => (editor.equals ?? defaultEquals)(a, b)

/** Entries whose value the rows should show in place of the server's. */
const showsValue = (entry: CellEntry) => entry.hasValue && !!entry.apply

/**
 * The rows with each held value applied, so a pending or failed edit shows
 * in its cell (and in anything else drawn from the row) until the server has
 * it. Untouched rows and subtrees keep their identity.
 *
 * Tree rows are rebuilt through `children`, the shape draft rows already
 * require of a tree grid.
 */
export function applyEntries<T>(
  data: T[],
  entries: CellEntries,
  rowKeyOf: (row: T) => string | undefined,
  isTree: boolean,
): T[] {
  const byRow = new Map<string, CellEntry[]>()
  for (const entry of Object.values(entries)) {
    if (!showsValue(entry)) continue
    const list = byRow.get(entry.rowKey)
    if (list) list.push(entry)
    else byRow.set(entry.rowKey, [entry])
  }
  if (byRow.size === 0) return data

  const patchRow = (row: T): T => {
    const key = rowKeyOf(row)
    const list = key === undefined ? undefined : byRow.get(key)
    if (!list) return row
    return list.reduce((acc, entry) => entry.apply!(acc, entry.value) as T, row)
  }

  if (!isTree) {
    let changed = false
    const next = data.map((row) => {
      const patched = patchRow(row)
      if (patched !== row) changed = true
      return patched
    })
    return changed ? next : data
  }

  const walk = (rows: T[]): T[] => {
    let changed = false
    const next = rows.map((row) => {
      let node = patchRow(row)
      const children = (node as { children?: T[] }).children
      if (Array.isArray(children) && children.length > 0) {
        const nextChildren = walk(children)
        if (nextChildren !== children)
          node = { ...node, children: nextChildren }
      }
      if (node !== row) changed = true
      return node
    })
    return changed ? next : rows
  }
  return walk(data)
}

/** Finds a row by key, through `children` in a tree. */
export function findRowByKey<T>(
  data: T[],
  key: string,
  rowKeyOf: (row: T) => string | undefined,
): T | undefined {
  for (const row of data) {
    if (rowKeyOf(row) === key) return row
    const children = (row as { children?: T[] }).children
    if (Array.isArray(children)) {
      const found = findRowByKey(children, key, rowKeyOf)
      if (found) return found
    }
  }
  return undefined
}

export interface ServerErrorTarget {
  columnId: string
  label: string
}

export interface MappedServerErrors {
  /** Message per column, already prefixed with the column label. */
  byColumn: Record<string, string>
  /** Messages no column claims, and the response's `detail` when it has no
   *  field errors. */
  unmatched: string[]
}

/**
 * Places a failed commit's errors on cells. A 422's keys are matched to
 * columns through `fieldToColumn` first, then by the field an editor writes
 * (ignoring the server's casing); anything left over is reported unmatched.
 */
export function mapServerErrors(
  error: unknown,
  targets: {
    /** Field (any casing) → the column whose editor writes it. */
    byField: Map<string, ServerErrorTarget>
    /** Column id → its label, for keys mapped by `fieldToColumn`. */
    byColumn: Map<string, ServerErrorTarget>
    fieldToColumn?: Record<string, string>
  },
): MappedServerErrors {
  const apiError = toApiError(error)
  const result: MappedServerErrors = { byColumn: {}, unmatched: [] }
  const errors = apiError.errors ?? {}
  const keys = Object.keys(errors)

  const lookup = (key: string): ServerErrorTarget | undefined => {
    const bare =
      key
        .replace(/^\$\.?/, '')
        .split('.')
        .pop() ?? key
    for (const candidate of [key, bare]) {
      const mapped = Object.entries(targets.fieldToColumn ?? {}).find(
        ([field]) => field.toLowerCase() === candidate.toLowerCase(),
      )?.[1]
      if (mapped) return targets.byColumn.get(mapped)
      const byField = targets.byField.get(candidate.toLowerCase())
      if (byField) return byField
    }
    return undefined
  }

  for (const key of keys) {
    const raw = errors[key] as string[] | string
    const message = (Array.isArray(raw) ? raw : [raw]).filter(Boolean).join(' ')
    if (!message) continue
    const target = lookup(key)
    if (!target) {
      result.unmatched.push(message)
      continue
    }
    const text = `${target.label}: ${message}`
    const existing = result.byColumn[target.columnId]
    result.byColumn[target.columnId] = existing
      ? `${existing} ${message}`
      : text
  }

  if (keys.length === 0) {
    result.unmatched.push(
      apiError.detail ??
        (error instanceof Error && error.message
          ? error.message
          : 'The change could not be saved.'),
    )
  }
  return result
}

/** A cell position, by display row index and on-screen column index. */
export interface CellPosition {
  rowIndex: number
  colIndex: number
}

/**
 * The next (or previous) editable cell in on-screen order, wrapping across
 * rows, or null past the grid's last (or first) editable cell.
 */
export function nextEditableCell(
  from: CellPosition,
  direction: 1 | -1,
  size: { rowCount: number; colCount: number },
  isEditable: (rowIndex: number, colIndex: number) => boolean,
): CellPosition | null {
  let { rowIndex, colIndex } = from
  const total = size.rowCount * size.colCount
  for (let step = 0; step < total; step++) {
    colIndex += direction
    if (colIndex >= size.colCount) {
      colIndex = 0
      rowIndex += 1
    } else if (colIndex < 0) {
      colIndex = size.colCount - 1
      rowIndex -= 1
    }
    if (rowIndex < 0 || rowIndex >= size.rowCount) return null
    if (isEditable(rowIndex, colIndex)) return { rowIndex, colIndex }
  }
  return null
}
