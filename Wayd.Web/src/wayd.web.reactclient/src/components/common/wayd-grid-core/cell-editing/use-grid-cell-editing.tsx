'use client'

import { ConfigProvider } from 'antd'
import {
  useEffect,
  useEffectEvent,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type RefObject,
} from 'react'
import type { RowData } from '@tanstack/react-table'
import type { Column, Row, Table } from '../index'
import type { DraftItem } from '../draft-utils'
import {
  applyEntries,
  cellKey,
  cellMessageId,
  editorEquals,
  editorField,
  findRowByKey,
  mapServerErrors,
  nextEditableCell,
  type CellEntries,
  type CellEntry,
  type ServerErrorTarget,
} from './edit-state'
import type { CellEditor, CellMark, EditorGroup } from './types'

/** The cell whose editor is open. */
export interface OpenCell {
  /** The row's data id — what `onCommit` receives. */
  rowKey: string
  columnId: string
}

/** One open editor, from the moment it opens until it commits or reverts. */
interface EditSession extends OpenCell {
  /** TanStack's id for the row, which the body's DOM is keyed by. */
  rowId: string
  field: string
  seq: number
  editor: CellEditor
  row: unknown
  initialValue: unknown
  initialInput?: string
  label: string
  isDraft: boolean
}

/** A cell to send, as one field of a patch. */
interface CommitCell {
  key: string
  field: string
  columnId: string
  value: unknown
  apply: CellEditor['apply']
}

/** What the body's key handler needs from the keyboard model. */
export interface CellEditingNavigation<T extends RowData> {
  /** The displayed rows, in display order. */
  rows: Row<T>[]
  /** Visible leaf column ids in on-screen order. */
  columnIds: string[]
  /** Moves the Tab stop and focus to a cell, by TanStack row id. */
  focusCell: (rowId: string, columnId: string) => void
  /** Hands focus to the sentinel before or after the body, so the browser's
   *  Tab step leaves the grid. */
  leaveGrid: (backwards: boolean) => void
}

/** What the grid gives {@link useGridCellEditing}. */
export interface GridCellEditingOptions<T extends RowData> {
  /** Some column declares an editor and the grid has `onCommit`. */
  enabled: boolean
  /** The rows, drafts included. Tree rows nest under `children`, as draft
   *  rows already require. */
  data: T[]
  /** The rows as the consumer passed them, without drafts: a change here is
   *  a change from the server. */
  serverData: T[]
  isTree: boolean
  /** The data id of a row: `getRowId`, else `row.id`. */
  rowKeyOf: (row: T) => string | undefined
  /** The id prefix that marks a draft row. */
  draftPrefix: string
  /** The grid can add draft rows (`createDraftNode` and `onCreate`). */
  canCreateDrafts: boolean
  /** Draft rows in the grid now; a new one waits for this to be zero. */
  draftCount: number
  insertDraft: (draft: DraftItem) => void
  onCommit?: (rowId: string, patch: Record<string, unknown>) => Promise<void>
  onCreate?: (
    parentId: string | null,
    values: Record<string, unknown>,
  ) => Promise<string | void>
  fieldToColumn?: Record<string, string>
  draftParentId: (draftKey: string) => string | null
  /** Drops a draft row — discarded by the user, or replaced by the row
   *  `onCreate` made. */
  removeDraft: (draftKey: string, created: boolean) => void
  /** Tells the user about a failure whose cell is not on screen. */
  notify?: (message: string) => void
  bodyViewportRef: RefObject<HTMLElement | null>
}

const INTERACTIVE_CONTENT =
  'a, button, input, select, textarea, [role="button"], [data-row-actions], [data-row-activate="ignore"]'

const columnLabel = <T extends RowData>(
  column: Column<T, unknown> | undefined,
  fallback: string,
) => {
  const header = column?.columnDef.header
  return typeof header === 'string' && header ? header : fallback
}

const editorOf = <T extends RowData>(column: Column<T, unknown> | undefined) =>
  column?.columnDef.meta?.editor as CellEditor | undefined

const escapeSelector = (value: string) =>
  typeof CSS !== 'undefined' && CSS.escape
    ? CSS.escape(value)
    : value.replace(/["\\]/g, '\\$&')

/**
 * Edits one cell at a time, in the grid core. A column opts in with
 * `meta.editor`; the grid opens the editor in place, commits the one field it
 * edits through `onCommit`, and marks the cell unsaved, saving or failed.
 *
 * What the user typed lives here, never in a form or the DOM: a refetch
 * cannot reset it, a hidden column cannot be read as cleared, and a failed
 * commit keeps the value on its cell. Commits run one at a time per row.
 */
export function useGridCellEditing<T extends RowData>({
  enabled,
  data,
  serverData,
  isTree,
  rowKeyOf,
  draftPrefix,
  canCreateDrafts,
  draftCount,
  insertDraft,
  onCommit,
  onCreate,
  fieldToColumn,
  draftParentId,
  removeDraft,
  notify,
  bodyViewportRef,
}: GridCellEditingOptions<T>) {
  const isDraftKey = (key: string) => key.startsWith(draftPrefix)
  const canCreateDraft = enabled && canCreateDrafts && draftCount === 0
  const [entries, setEntries] = useState<CellEntries>({})
  const [session, setSession] = useState<EditSession | null>(null)
  const [pendingOpen, setPendingOpen] = useState<OpenCell | null>(null)
  const [pendingFocus, setPendingFocus] = useState<OpenCell | null>(null)
  const [announcement, setAnnouncement] = useState('')
  const idPrefix = useId()
  const messageId = (rowKey: string, columnId: string) =>
    cellMessageId(idPrefix, rowKey, columnId)

  const entriesRef = useRef<CellEntries>(entries)
  const sessionRef = useRef<EditSession | null>(null)
  const editorValueRef = useRef<unknown>(undefined)
  const popupOpenRef = useRef(false)
  const popupHostRef = useRef<HTMLDivElement | null>(null)
  const seqRef = useRef(0)
  const queuesRef = useRef(new Map<string, Promise<void>>())
  const creatingRef = useRef(new Set<string>())
  // Rows `onCreate` made that the data does not show yet; their entries must
  // outlive the data changes before the refetch brings them in.
  const awaitedRowsRef = useRef(new Set<string>())
  const dataVersionRef = useRef(0)
  const clickStartRef = useRef<HTMLElement | null>(null)
  const tableRef = useRef<Table<T> | null>(null)
  // Read by handlers that run after a commit resolves, when the props they
  // close over may be several renders old.
  const latestRef = useRef({
    data,
    onCommit,
    onCreate,
    fieldToColumn,
    draftParentId,
    removeDraft,
    notify,
    rowKeyOf,
  })
  useLayoutEffect(() => {
    latestRef.current = {
      data,
      onCommit,
      onCreate,
      fieldToColumn,
      draftParentId,
      removeDraft,
      notify,
      rowKeyOf,
    }
  })

  const writeEntries = (update: (prev: CellEntries) => CellEntries) => {
    const next = update(entriesRef.current)
    if (next === entriesRef.current) return
    entriesRef.current = next
    setEntries(next)
  }

  const nextSeq = () => ++seqRef.current

  const rawRow = (rowKey: string) =>
    findRowByKey(latestRef.current.data, rowKey, latestRef.current.rowKeyOf)

  const leafColumns = () =>
    (tableRef.current?.getAllLeafColumns() ?? []) as Column<T, unknown>[]

  const columnById = (columnId: string) =>
    leafColumns().find((column) => column.id === columnId)

  const labelOf = (columnId: string) =>
    columnLabel(columnById(columnId), columnId)

  // ─── Settled values give way to the server's ────────────────────────────
  // A committed value stays on its cell until the data shows it, so the old
  // value never flashes back while the consumer refetches. A value the server
  // normalized (trimmed, rounded) never matches; it gives way one data change
  // later.
  const onServerDataChange = useEffectEvent(() => {
    dataVersionRef.current += 1
    const version = dataVersionRef.current
    const rows = latestRef.current.data
    for (const key of awaitedRowsRef.current) {
      if (findRowByKey(rows, key, rowKeyOf)) awaitedRowsRef.current.delete(key)
    }
    writeEntries((prev) => {
      let changed = false
      const next: Record<string, CellEntry> = { ...prev }
      for (const [key, entry] of Object.entries(prev)) {
        const row = findRowByKey(rows, entry.rowKey, rowKeyOf)
        if (!row) {
          if (
            creatingRef.current.has(entry.rowKey) ||
            awaitedRowsRef.current.has(entry.rowKey)
          ) {
            continue
          }
          delete next[key]
          changed = true
          continue
        }
        if (entry.phase !== 'settled') continue
        const editor = editorOf(columnById(entry.columnId))
        const confirmed =
          !editor || editorEquals(editor, editor.get(row), entry.value)
        if (confirmed || (entry.settledAt ?? 0) < version - 1) {
          delete next[key]
          changed = true
        }
      }
      return changed ? next : prev
    })
  })
  // Keyed on the server's rows, not the drafts merged into them: adding or
  // dropping a draft is not news from the server.
  useEffect(() => onServerDataChange(), [serverData])

  // ─── Leaving the page ───────────────────────────────────────────────────
  useEffect(() => {
    if (!enabled) return
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      const open = sessionRef.current
      const openIsDirty =
        !!open &&
        !editorEquals(open.editor, editorValueRef.current, open.initialValue)
      // A server error mark on a cell the user did not change holds nothing
      // to lose.
      const pending = Object.values(entriesRef.current).some(
        (entry) =>
          entry.phase === 'saving' ||
          (entry.hasValue && entry.phase !== 'settled'),
      )
      if (!openIsDirty && !pending) return
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [enabled])

  // An editor's popup host is removed with the grid if the grid unmounts
  // while the editor is open.
  useEffect(
    () => () => {
      popupHostRef.current?.remove()
      popupHostRef.current = null
    },
    [],
  )

  // ─── Finding cells ──────────────────────────────────────────────────────
  const displayedRows = () =>
    (tableRef.current?.getRowModel().rows ?? []) as Row<T>[]

  const rowIdFor = (rowKey: string) =>
    displayedRows().find((row) => rowKeyOf(row.original) === rowKey)?.id

  const cellElement = (rowKey: string, columnId: string) => {
    const rowId = rowIdFor(rowKey)
    if (!rowId) return null
    return (
      bodyViewportRef.current?.querySelector<HTMLElement>(
        `tr[data-grid-row="${escapeSelector(rowId)}"] [data-grid-cell][data-column-id="${escapeSelector(columnId)}"]`,
      ) ?? null
    )
  }

  /** The grid cell an element sits in, as a row key and column. */
  const cellAt = (element: Element | null): OpenCell | null => {
    const td = element?.closest?.('[data-grid-cell]')
    const rowId = td?.closest('tr')?.getAttribute('data-grid-row')
    const columnId = td?.getAttribute('data-column-id')
    if (!rowId || !columnId) return null
    const row = displayedRows().find((r) => r.id === rowId)
    const rowKey = row ? rowKeyOf(row.original) : undefined
    return rowKey ? { rowKey, columnId } : null
  }

  // ─── Which cells edit ───────────────────────────────────────────────────
  const editorFor = (row: Row<T>, columnId: string) =>
    editorOf(
      row.getAllCells().find((cell) => cell.column.id === columnId)?.column,
    )

  const isCellEditable = (row: Row<T>, columnId: string): boolean => {
    if (!enabled || row.getIsGrouped()) return false
    const editor = editorFor(row, columnId)
    if (!editor) return false
    const key = rowKeyOf(row.original)
    if (!key) return false
    return editor.editable?.(row.original, { isDraft: isDraftKey(key) }) ?? true
  }

  /** The column whose commit creates a draft: the tree column, else the
   *  first column with an editor. */
  const draftColumnId = () => {
    const columns = leafColumns().filter((column) => editorOf(column))
    return (
      columns.find((column) => column.columnDef.meta?.treeColumn)?.id ??
      columns[0]?.id
    )
  }

  // ─── Opening and closing ────────────────────────────────────────────────
  const openCell = (row: Row<T>, columnId: string, initialInput?: string) => {
    if (!isCellEditable(row, columnId)) return false
    const rowKey = rowKeyOf(row.original)!
    const current = sessionRef.current
    if (current?.rowKey === rowKey && current.columnId === columnId) return true
    if (current) commitOpen({ next: { rowKey, columnId }, focus: false })

    const column = row
      .getAllCells()
      .find((cell) => cell.column.id === columnId)?.column
    const editor = editorOf(column)!
    const value = editor.get(row.original)
    const initialValue =
      initialInput !== undefined && editor.fromInput
        ? editor.fromInput(initialInput, row.original)
        : editor.format
          ? editor.format(value, row.original)
          : value

    const host = document.createElement('div')
    host.setAttribute('data-grid-editor-owner', `${rowKey}:${columnId}`)
    document.body.appendChild(host)
    popupHostRef.current = host
    popupOpenRef.current = false
    editorValueRef.current = initialValue

    const next: EditSession = {
      rowKey,
      rowId: row.id,
      columnId,
      field: editorField(editor, row.original),
      seq: nextSeq(),
      editor,
      row: row.original,
      initialValue,
      initialInput: editor.fromInput ? initialInput : undefined,
      label: columnLabel(column, columnId),
      isDraft: isDraftKey(rowKey),
    }
    sessionRef.current = next
    setSession(next)
    return true
  }

  const closeSession = () => {
    sessionRef.current = null
    setSession(null)
    popupHostRef.current?.remove()
    popupHostRef.current = null
  }

  /** The open editor's cell, looked up afresh: the row's element can be
   *  replaced while the editor stays open. */
  const sessionCell = (open: EditSession | null) =>
    open
      ? (bodyViewportRef.current?.querySelector<HTMLElement>(
          `tr[data-grid-row="${escapeSelector(open.rowId)}"] [data-grid-cell][data-column-id="${escapeSelector(open.columnId)}"]`,
        ) ?? null)
      : null

  const focusSessionCell = (open: EditSession) =>
    sessionCell(open)?.focus({ preventScroll: true })

  /** Closes the editor and commits what it holds. `next` is where the user
   *  is going: a pair stays unsaved while they move within it. */
  const commitOpen = ({
    next,
    focus,
  }: {
    next: OpenCell | null
    focus: boolean
  }) => {
    const open = sessionRef.current
    if (!open) return
    const editorValue = editorValueRef.current
    closeSession()
    const value = open.editor.parse
      ? open.editor.parse(editorValue, open.row)
      : editorValue
    finishCell(open, value, next)
    if (focus) focusSessionCell(open)
  }

  /** Closes the editor without committing; an empty draft goes with it. */
  const cancelOpen = () => {
    const open = sessionRef.current
    if (!open) return
    closeSession()
    // A draft's pair is held until the draft is created, never sent.
    if (open.editor.group && !open.isDraft) {
      flushGroup(open.rowKey, open.editor.group)
    }
    if (open.isDraft && !draftHasValues(open.rowKey)) {
      discardDraftRow(open.rowKey, false, open.columnId)
      return
    }
    focusSessionCell(open)
  }

  // ─── Committing ─────────────────────────────────────────────────────────
  const setEntry = (key: string, entry: CellEntry | null) =>
    writeEntries((prev) => {
      if (!entry) {
        if (!(key in prev)) return prev
        const next = { ...prev }
        delete next[key]
        return next
      }
      return { ...prev, [key]: entry }
    })

  const finishCell = (
    open: EditSession,
    value: unknown,
    next: OpenCell | null,
  ) => {
    const { editor, rowKey, field, columnId } = open
    const raw = rawRow(rowKey) ?? (open.row as T)
    const key = cellKey(rowKey, field)
    const existing = entriesRef.current[key]
    const inFlight =
      existing?.hasValue &&
      (existing.phase === 'saving' || existing.phase === 'settled')
    const baseline = inFlight ? existing.value : editor.get(raw)
    const changed = !editorEquals(editor, value, baseline)
    const held = (phase: CellEntry['phase'], error?: string): CellEntry => ({
      rowKey,
      field,
      columnId,
      phase,
      value,
      hasValue: true,
      error,
      seq: nextSeq(),
      apply: editor.apply,
    })

    if (open.isDraft) {
      if (!changed) {
        if (!inFlight) setEntry(key, null)
        markMissingToCreate(rowKey)
        return
      }
      const error = editor.validate?.(value as never, raw)
      setEntry(
        key,
        held(error ? 'error' : 'unsaved', error && `${open.label}: ${error}`),
      )
      if (markMissingToCreate(rowKey).length === 0 && !error) {
        createDraft(rowKey)
      }
      return
    }

    if (editor.group) {
      if (!inFlight) setEntry(key, changed ? held('unsaved') : null)
      else if (changed) setEntry(key, held('unsaved'))
      const nextColumn =
        next?.rowKey === rowKey ? columnById(next.columnId) : undefined
      if (editorOf(nextColumn)?.group?.id === editor.group.id) return
      flushGroup(rowKey, editor.group)
      return
    }

    if (!changed) {
      if (!inFlight) setEntry(key, null)
      return
    }
    const error = editor.validate?.(value as never, raw)
    if (error) {
      setEntry(key, held('error', `${open.label}: ${error}`))
      return
    }
    send(rowKey, [{ key, field, columnId, value, apply: editor.apply }])
  }

  /** Checks a pair's held values together and sends the changed ones. */
  const flushGroup = (rowKey: string, group: EditorGroup) => {
    const raw = rawRow(rowKey)
    if (!raw) return
    const members = leafColumns()
      .map((column) => ({ column, editor: editorOf(column) }))
      .filter(
        (
          member,
        ): member is { column: Column<T, unknown>; editor: CellEditor } =>
          member.editor?.group?.id === group.id,
      )

    const values: Record<string, unknown> = {}
    const changed: Array<CommitCell & { editor: CellEditor; label: string }> =
      []
    const held: string[] = []
    for (const { column, editor } of members) {
      const field = editorField(editor, raw)
      const key = cellKey(rowKey, field)
      const entry = entriesRef.current[key]
      const isHeld =
        !!entry && (entry.phase === 'unsaved' || entry.phase === 'error')
      if (isHeld) held.push(key)
      const value = entry?.hasValue ? entry.value : editor.get(raw)
      if (editor.role) values[editor.role] = value
      if (
        isHeld &&
        entry.hasValue &&
        !editorEquals(editor, entry.value, editor.get(raw))
      ) {
        changed.push({
          key,
          field,
          columnId: column.id,
          value: entry.value,
          apply: editor.apply,
          editor,
          label: columnLabel(column, column.id),
        })
      }
    }

    if (changed.length === 0) {
      writeEntries((prev) => {
        const next = { ...prev }
        for (const key of held) delete next[key]
        return held.length ? next : prev
      })
      return
    }

    const errors: Record<string, string> = {}
    for (const cell of changed) {
      const error = cell.editor.validate?.(cell.value as never, raw)
      if (error) errors[cell.columnId] = `${cell.label}: ${error}`
    }
    const groupErrors = group.validate?.(values, raw) ?? {}
    for (const [role, message] of Object.entries(groupErrors)) {
      const member = members.find((m) => m.editor.role === role)
      if (member && message && !errors[member.column.id]) {
        errors[member.column.id] =
          `${columnLabel(member.column, member.column.id)}: ${message}`
      }
    }

    if (Object.keys(errors).length > 0) {
      writeEntries((prev) => {
        const next = { ...prev }
        for (const { column, editor } of members) {
          const field = editorField(editor, raw)
          const key = cellKey(rowKey, field)
          const entry = prev[key]
          const message = errors[column.id]
          if (message) {
            next[key] = entry?.hasValue
              ? { ...entry, phase: 'error', error: message, seq: nextSeq() }
              : {
                  rowKey,
                  field,
                  columnId: column.id,
                  phase: 'error',
                  hasValue: false,
                  error: message,
                  seq: nextSeq(),
                }
          } else if (entry?.hasValue && entry.phase === 'error') {
            next[key] = { ...entry, phase: 'unsaved', error: undefined }
          } else if (entry && !entry.hasValue) {
            delete next[key]
          }
        }
        return next
      })
      return
    }

    // Error-only marks on members that did not change are now resolved.
    writeEntries((prev) => {
      const next = { ...prev }
      for (const key of held) {
        if (!changed.some((cell) => cell.key === key)) delete next[key]
      }
      return next
    })
    send(rowKey, changed)
  }

  /** Queues one patch for a row behind any commit already running for it. */
  const send = (rowKey: string, cells: CommitCell[]) => {
    const commit = latestRef.current.onCommit
    if (!commit || cells.length === 0) return
    const seqs = cells.map(() => nextSeq())
    writeEntries((prev) => {
      const next = { ...prev }
      cells.forEach((cell, i) => {
        next[cell.key] = {
          rowKey,
          field: cell.field,
          columnId: cell.columnId,
          phase: 'saving',
          value: cell.value,
          hasValue: true,
          seq: seqs[i],
          apply: cell.apply,
        }
      })
      return next
    })
    const patch = Object.fromEntries(
      cells.map((cell) => [cell.field, cell.value]),
    )
    const previous = queuesRef.current.get(rowKey) ?? Promise.resolve()
    const run = previous
      .then(() => latestRef.current.onCommit!(rowKey, patch))
      .then(
        () => settle(cells, seqs),
        (error: unknown) => fail(rowKey, cells, seqs, error),
      )
    queuesRef.current.set(rowKey, run)
    void run.then(() => {
      if (queuesRef.current.get(rowKey) === run)
        queuesRef.current.delete(rowKey)
    })
  }

  const settle = (cells: CommitCell[], seqs: number[]) =>
    writeEntries((prev) => {
      const next = { ...prev }
      cells.forEach((cell, i) => {
        const entry = prev[cell.key]
        if (entry?.seq !== seqs[i]) return
        next[cell.key] = {
          ...entry,
          phase: 'settled',
          error: undefined,
          settledAt: dataVersionRef.current,
        }
      })
      return next
    })

  const errorTargets = () => {
    const byField = new Map<string, ServerErrorTarget>()
    const byColumn = new Map<string, ServerErrorTarget>()
    for (const column of leafColumns()) {
      const target = {
        columnId: column.id,
        label: columnLabel(column, column.id),
      }
      byColumn.set(column.id, target)
      const editor = editorOf(column)
      if (editor && typeof editor.field === 'string') {
        byField.set(editor.field.toLowerCase(), target)
      }
    }
    return { byField, byColumn, fieldToColumn: latestRef.current.fieldToColumn }
  }

  /** Marks a failed commit's cells, keeping the user's values. */
  const fail = (
    rowKey: string,
    cells: CommitCell[],
    seqs: number[],
    error: unknown,
  ) => {
    const targets = errorTargets()
    // A field written per row (a milestone's date) is matched by this row.
    for (const cell of cells) {
      targets.byField.set(cell.field.toLowerCase(), {
        columnId: cell.columnId,
        label: labelOf(cell.columnId),
      })
    }
    const mapped = mapServerErrors(error, targets)
    const fallback =
      mapped.unmatched.join(' ') || 'The change could not be saved.'
    const raw = rawRow(rowKey)
    const messages: string[] = []
    writeEntries((prev) => {
      const next = { ...prev }
      cells.forEach((cell, i) => {
        const entry = prev[cell.key]
        if (entry?.seq !== seqs[i]) return
        const message =
          mapped.byColumn[cell.columnId] ??
          `${labelOf(cell.columnId)}: ${fallback}`
        messages.push(message)
        next[cell.key] = { ...entry, phase: 'error', error: message }
      })
      for (const [columnId, message] of Object.entries(mapped.byColumn)) {
        if (cells.some((cell) => cell.columnId === columnId)) continue
        const editor = editorOf(columnById(columnId))
        if (!editor || !raw) continue
        const field = editorField(editor, raw)
        const key = cellKey(rowKey, field)
        const entry = next[key]
        next[key] = entry?.hasValue
          ? { ...entry, phase: 'error', error: message }
          : {
              rowKey,
              field,
              columnId,
              phase: 'error',
              hasValue: false,
              error: message,
              seq: nextSeq(),
            }
      }
      return next
    })
    if (messages.length > 0) {
      afterFailure(rowKey, cells[0].columnId, messages[0])
      setAnnouncement(`Not saved. ${messages.join(' ')}`)
    }
  }

  /** Takes the user to a failed cell, or says so when it is not on screen. */
  const afterFailure = (rowKey: string, columnId: string, message: string) => {
    const td = cellElement(rowKey, columnId)
    if (!td) {
      latestRef.current.notify?.(message)
      return
    }
    const active = document.activeElement
    const focusInBody = !!active && !!bodyViewportRef.current?.contains(active)
    if (focusInBody && !sessionRef.current) td.focus({ preventScroll: true })
  }

  // ─── Drafts ─────────────────────────────────────────────────────────────
  const draftHasValues = (draftKey: string) =>
    Object.values(entriesRef.current).some(
      (entry) => entry.rowKey === draftKey && entry.hasValue,
    )

  /**
   * Drops a draft row and its entries. A draft the user discarded hands focus
   * to the row that takes its place (or the one above), in `columnId`.
   */
  const discardDraftRow = (
    draftKey: string,
    created = false,
    columnId?: string,
  ) => {
    if (!created && columnId) {
      const rows = displayedRows()
      const index = rows.findIndex((row) => rowKeyOf(row.original) === draftKey)
      const neighbour = rows[index + 1] ?? rows[index - 1]
      const neighbourKey = neighbour ? rowKeyOf(neighbour.original) : undefined
      if (neighbourKey) setPendingFocus({ rowKey: neighbourKey, columnId })
    }
    writeEntries((prev) => {
      const next = { ...prev }
      let changed = false
      for (const [key, entry] of Object.entries(prev)) {
        if (entry.rowKey !== draftKey) continue
        delete next[key]
        changed = true
      }
      return changed ? next : prev
    })
    latestRef.current.removeDraft(draftKey, created)
  }

  /** The draft as shown, with the values held on its cells. */
  const shownDraft = (draftKey: string) =>
    findRowByKey(
      applyEntries(
        latestRef.current.data,
        entriesRef.current,
        rowKeyOf,
        isTree,
      ),
      draftKey,
      rowKeyOf,
    )

  /** A draft cell's value: the one held on it, else the draft's own. */
  const draftValue = (draftKey: string, editor: CellEditor, draft: T) => {
    const entry =
      entriesRef.current[cellKey(draftKey, editorField(editor, draft))]
    return entry?.hasValue ? entry.value : editor.get(draft)
  }

  const requiredMessage = (label: string) =>
    `${label}: Required to create the item.`

  /**
   * The draft's cells that still need a value before it is created, in
   * column order: its Name while empty or invalid, and every shown cell
   * marked `requiredToCreate` that is editable on it. A hidden one is left
   * to the server's validation, whose error is toasted as off screen.
   */
  const missingToCreate = (draftKey: string): string[] => {
    const draft = shownDraft(draftKey)
    if (!draft) return []
    const nameColumn = draftColumnId()
    return leafColumns()
      .filter((column) => {
        const editor = editorOf(column)
        if (!editor) return false
        const isName = column.id === nameColumn
        if (!isName && (!editor.requiredToCreate || !column.getIsVisible())) {
          return false
        }
        if (editor.editable && !editor.editable(draft, { isDraft: true })) {
          return false
        }
        const value = draftValue(draftKey, editor, draft)
        if (editorEquals(editor, value, null)) return true
        return isName && !!editor.validate?.(value as never, draft)
      })
      .map((column) => column.id)
  }

  /**
   * Once a draft has a Name, marks the required cells it still lacks, so the
   * user sees why it was not created; clears marks a value has since filled
   * or that no longer apply. Returns every cell still missing.
   */
  const markMissingToCreate = (draftKey: string): string[] => {
    const missing = missingToCreate(draftKey)
    const draft = shownDraft(draftKey)
    const nameColumn = draftColumnId()
    if (!draft) return missing
    const named = !!nameColumn && !missing.includes(nameColumn)
    const added: string[] = []
    writeEntries((prev) => {
      let next: CellEntries = prev
      const edit = (): Record<string, CellEntry> =>
        next === prev
          ? (next = { ...prev })
          : (next as Record<string, CellEntry>)
      for (const column of leafColumns()) {
        const editor = editorOf(column)
        if (!editor?.requiredToCreate) continue
        const field = editorField(editor, draft)
        const key = cellKey(draftKey, field)
        const entry = prev[key]
        const message = requiredMessage(columnLabel(column, column.id))
        const flag = named && missing.includes(column.id)
        if (flag && !entry) {
          added.push(message)
          edit()[key] = {
            rowKey: draftKey,
            field,
            columnId: column.id,
            phase: 'error',
            hasValue: false,
            error: message,
            seq: nextSeq(),
          }
        } else if (
          !flag &&
          entry &&
          !entry.hasValue &&
          entry.error === message
        ) {
          delete edit()[key]
        }
      }
      return next
    })
    if (added.length > 0) setAnnouncement(`Not created yet. ${added.join(' ')}`)
    return missing
  }

  /** Marks cells as failed by column, keeping any value they hold. */
  const markErrors = (rowKey: string, row: T, errors: Record<string, string>) =>
    writeEntries((prev) => {
      const next = { ...prev }
      for (const [columnId, message] of Object.entries(errors)) {
        const editor = editorOf(columnById(columnId))
        if (!editor) continue
        const field = editorField(editor, row)
        const key = cellKey(rowKey, field)
        const entry = next[key]
        next[key] = entry?.hasValue
          ? { ...entry, phase: 'error', error: message, seq: nextSeq() }
          : {
              rowKey,
              field,
              columnId,
              phase: 'error',
              hasValue: false,
              error: message,
              seq: nextSeq(),
            }
      }
      return next
    })

  /**
   * Runs each pair's check over a draft's values, as an existing row's pair
   * is checked before it is sent. Marks what fails and says whether any did.
   */
  const draftFailsPairCheck = (draftKey: string, draft: T): boolean => {
    const groups = new Map<
      object,
      {
        group: EditorGroup
        members: { column: Column<T, unknown>; editor: CellEditor }[]
      }
    >()
    for (const column of leafColumns()) {
      const editor = editorOf(column)
      if (!editor?.group?.validate) continue
      const entry = groups.get(editor.group.id) ?? {
        group: editor.group,
        members: [],
      }
      entry.members.push({ column, editor })
      groups.set(editor.group.id, entry)
    }
    const errors: Record<string, string> = {}
    for (const { group, members } of groups.values()) {
      const values: Record<string, unknown> = {}
      for (const { editor } of members) {
        if (editor.role)
          values[editor.role] = draftValue(draftKey, editor, draft)
      }
      const result = group.validate!(values, draft) ?? {}
      for (const [role, message] of Object.entries(result)) {
        const member = members.find((m) => m.editor.role === role)
        if (member && message) {
          errors[member.column.id] =
            `${columnLabel(member.column, member.column.id)}: ${message}`
        }
      }
    }
    if (Object.keys(errors).length === 0) return false
    markErrors(draftKey, draft, errors)
    setAnnouncement(`Not created. ${Object.values(errors).join(' ')}`)
    return true
  }

  /** Creates a draft from the values held on its cells, once. */
  const createDraft = (draftKey: string) => {
    const create = latestRef.current.onCreate
    if (!create || creatingRef.current.has(draftKey)) return
    const held = Object.entries(entriesRef.current).filter(
      ([, entry]) => entry.rowKey === draftKey && entry.hasValue,
    )
    // Only a value that still fails its own check blocks the create; a mark
    // the server left after an earlier failed create is retried.
    const draft = rawRow(draftKey)
    const invalid = held.some(([, entry]) => {
      if (entry.phase !== 'error') return false
      const editor = editorOf(columnById(entry.columnId))
      return !!draft && !!editor?.validate?.(entry.value as never, draft)
    })
    if (invalid) return
    if (draft && draftFailsPairCheck(draftKey, draft)) return
    creatingRef.current.add(draftKey)
    const sent = held.map(([key, entry]) => ({ key, seq: nextSeq(), entry }))
    writeEntries((prev) => {
      const next = { ...prev }
      for (const { key, seq, entry } of sent) {
        next[key] = { ...entry, phase: 'saving', seq }
      }
      return next
    })
    const values = Object.fromEntries(
      sent.map(({ entry }) => [entry.field, entry.value]),
    )
    const sentKeys = new Set(sent.map(({ key }) => key))
    void create(latestRef.current.draftParentId(draftKey), values).then(
      (newId) => {
        // An editor still open on the draft closes into a held value, so
        // what the user is typing follows the new row too. The draft counts
        // as creating until then, so that close cannot start a second create.
        const open = sessionRef.current
        let columnId = draftColumnId() ?? ''
        if (open?.rowKey === draftKey) {
          columnId = open.columnId
          const editorValue = editorValueRef.current
          closeSession()
          const value = open.editor.parse
            ? open.editor.parse(editorValue, open.row)
            : editorValue
          finishCell(open, value, null)
        }
        creatingRef.current.delete(draftKey)
        // Values the user held after the create went out follow the new row.
        const later = Object.entries(entriesRef.current).filter(
          ([key, entry]) =>
            entry.rowKey === draftKey &&
            entry.hasValue &&
            !sentKeys.has(key) &&
            (entry.phase === 'unsaved' || entry.phase === 'error'),
        )
        discardDraftRow(draftKey, true)
        if (!newId) {
          if (later.length > 0) {
            latestRef.current.notify?.(
              'The new row was created, but later changes to it were not saved.',
            )
          }
          return
        }
        awaitedRowsRef.current.add(newId)
        const valid = later.filter(([, entry]) => entry.phase === 'unsaved')
        const invalid = later.filter(([, entry]) => entry.phase === 'error')
        // A value that failed its own check stays on its cell, still unsent.
        writeEntries((prev) => {
          if (invalid.length === 0) return prev
          const next = { ...prev }
          for (const [, entry] of invalid) {
            next[cellKey(newId, entry.field)] = { ...entry, rowKey: newId }
          }
          return next
        })
        send(
          newId,
          valid.map(([, entry]) => ({
            key: cellKey(newId, entry.field),
            field: entry.field,
            columnId: entry.columnId,
            value: entry.value,
            apply: entry.apply!,
          })),
        )
        setPendingFocus({ rowKey: newId, columnId })
      },
      (error: unknown) => {
        creatingRef.current.delete(draftKey)
        fail(
          draftKey,
          sent.map(({ key, entry }) => ({
            key,
            field: entry.field,
            columnId: entry.columnId,
            value: entry.value,
            apply: entry.apply!,
          })),
          sent.map(({ seq }) => seq),
          error,
        )
      },
    )
  }

  const draftCounterRef = useRef(0)
  /**
   * Adds a draft row under `parentId` (or at the root) and opens its first
   * cell once the row renders. One draft at a time.
   */
  const addDraft = (parentId?: string): string | null => {
    if (!canCreateDraft) return null
    draftCounterRef.current += 1
    const draft: DraftItem = {
      id: `${draftPrefix}${Date.now()}-${draftCounterRef.current}`,
      parentId,
      order: 0,
    }
    insertDraft(draft)
    if (parentId) {
      const parent = displayedRows().find(
        (row) => rowKeyOf(row.original) === parentId,
      )
      if (parent && !parent.getIsExpanded()) parent.toggleExpanded(true)
    }
    const columnId = draftColumnId()
    if (columnId) setPendingOpen({ rowKey: draft.id, columnId })
    return draft.id
  }

  // ─── Keys ───────────────────────────────────────────────────────────────
  /**
   * Edit-mode keys and the keys that open a cell. Runs in the capture phase
   * so it sees Escape and Enter before an open list handles them, and it
   * stops the keys it takes so neither the control nor navigation acts on
   * them twice.
   */
  const onKeyDownCapture = (
    e: React.KeyboardEvent<HTMLElement>,
    nav: CellEditingNavigation<T>,
  ) => {
    if (!enabled) return
    const target = e.target as HTMLElement
    // Keys from portals bubble here through the React tree.
    if (!e.currentTarget.contains(target)) return
    const open = sessionRef.current

    const take = () => {
      e.preventDefault()
      e.stopPropagation()
    }

    if (open && target.closest('[data-grid-cell-editor]')) {
      if (e.nativeEvent.isComposing) return
      const popupOpen = popupOpenRef.current
      const rowIndex = nav.rows.findIndex((row) => row.id === open.rowId)
      switch (e.key) {
        case 'Escape':
          if (popupOpen) return
          take()
          cancelOpen()
          return
        case 'Enter': {
          if (popupOpen || e.shiftKey || e.altKey) return
          take()
          const below = rowIndex >= 0 ? nav.rows[rowIndex + 1] : undefined
          const belowKey = below ? rowKeyOf(below.original) : undefined
          commitOpen({
            next: belowKey
              ? { rowKey: belowKey, columnId: open.columnId }
              : null,
            focus: !below,
          })
          // A named draft that still lacks a value it needs opens that cell
          // next, rather than being left behind.
          if (
            open.isDraft &&
            open.columnId === draftColumnId() &&
            draftHasValues(open.rowKey)
          ) {
            const missing = missingToCreate(open.rowKey)
            const nextMissing = nav.columnIds.find((id) => missing.includes(id))
            const draftRow = nav.rows[rowIndex]
            if (nextMissing && draftRow && !missing.includes(open.columnId)) {
              nav.focusCell(draftRow.id, nextMissing)
              openCell(draftRow, nextMissing)
              return
            }
          }
          if (below) nav.focusCell(below.id, open.columnId)
          return
        }
        case 'Tab': {
          // Neither the control (rc-select picks an option on Tab) nor
          // navigation may act on it.
          e.stopPropagation()
          const colIndex = nav.columnIds.indexOf(open.columnId)
          const target =
            rowIndex >= 0 && colIndex >= 0
              ? nextEditableCell(
                  { rowIndex, colIndex },
                  e.shiftKey ? -1 : 1,
                  { rowCount: nav.rows.length, colCount: nav.columnIds.length },
                  (r, c) => isCellEditable(nav.rows[r], nav.columnIds[c]),
                )
              : null
          if (!target) {
            // Past the last editable cell: leave the grid. The browser's
            // own Tab step runs on from the sentinel, so it is not prevented.
            commitOpen({ next: null, focus: true })
            nav.leaveGrid(e.shiftKey)
            return
          }
          e.preventDefault()
          const row = nav.rows[target.rowIndex]
          const columnId = nav.columnIds[target.colIndex]
          commitOpen({
            next: { rowKey: rowKeyOf(row.original)!, columnId },
            focus: false,
          })
          nav.focusCell(row.id, columnId)
          openCell(row, columnId)
          return
        }
      }
      return
    }

    if (!target.hasAttribute('data-grid-cell')) return
    const rowId = target.closest('tr')?.getAttribute('data-grid-row')
    const row = nav.rows.find((r) => r.id === rowId)
    const columnId = target.getAttribute('data-column-id')
    if (!row || !columnId) return

    if (e.key === 'Escape') {
      if (escapeOnCell(row, columnId)) take()
      return
    }
    if (e.key === 'Enter' || e.key === 'F2') {
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey || e.altKey)) return
      if (!isCellEditable(row, columnId)) return
      take()
      openCell(row, columnId)
      return
    }
    const printable =
      e.key.length === 1 &&
      e.key !== ' ' &&
      !e.ctrlKey &&
      !e.metaKey &&
      !e.altKey
    if (printable && isCellEditable(row, columnId)) {
      take()
      openCell(row, columnId, e.key)
    }
  }

  /** Escape on a cell in navigation mode: discards a held or failed value,
   *  or an empty draft. */
  const escapeOnCell = (row: Row<T>, columnId: string) => {
    const rowKey = rowKeyOf(row.original)
    if (!rowKey) return false
    const editor = editorFor(row, columnId)
    if (editor) {
      const key = cellKey(rowKey, editorField(editor, row.original))
      const entry = entriesRef.current[key]
      // A draft's "required" mark holds no value to discard; Escape on it
      // must leave the other member's held value alone.
      const requiredMark =
        !!entry &&
        !entry.hasValue &&
        entry.error === requiredMessage(labelOf(columnId))
      if (
        entry &&
        !requiredMark &&
        (entry.phase === 'error' || entry.phase === 'unsaved')
      ) {
        if (editor.group) {
          const groupId = editor.group.id
          const memberColumns = new Set(
            leafColumns()
              .filter((column) => editorOf(column)?.group?.id === groupId)
              .map((column) => column.id),
          )
          writeEntries((prev) => {
            const next = { ...prev }
            for (const [k, e] of Object.entries(prev)) {
              if (
                e.rowKey === rowKey &&
                memberColumns.has(e.columnId) &&
                (e.phase === 'error' || e.phase === 'unsaved')
              ) {
                delete next[k]
              }
            }
            return next
          })
        } else {
          setEntry(key, null)
        }
        return true
      }
    }
    if (
      isDraftKey(rowKey) &&
      !creatingRef.current.has(rowKey) &&
      !draftHasValues(rowKey)
    ) {
      discardDraftRow(rowKey, false, columnId)
      return true
    }
    return false
  }

  // ─── Pointer ────────────────────────────────────────────────────────────
  // A pointerdown anywhere outside the open cell and the popups it owns
  // commits it — the body's empty space, a header, the toolbar, a chart pane.
  const onDocumentPointerDown = useEffectEvent((event: PointerEvent) => {
    const target = event.target as Node | null
    if (!target) return
    if (sessionCell(sessionRef.current)?.contains(target)) return
    if (popupHostRef.current?.contains(target)) return
    // No `next`: a click selects a cell without opening it, so a pair is
    // checked and sent now rather than held for an editor that never opens.
    commitOpen({ next: null, focus: false })
  })
  const isOpen = !!session
  useEffect(() => {
    if (!isOpen) return
    const onPointerDown = (event: PointerEvent) => onDocumentPointerDown(event)
    document.addEventListener('pointerdown', onPointerDown, true)
    return () =>
      document.removeEventListener('pointerdown', onPointerDown, true)
  }, [isOpen])

  /** Remembers whether a click began on the already-focused cell, which
   *  is what makes it open the cell rather than select it. */
  const onPointerDownCapture = (e: React.PointerEvent<HTMLElement>) => {
    const td = (e.target as Element).closest?.<HTMLElement>('[data-grid-cell]')
    clickStartRef.current = td && document.activeElement === td ? td : null
  }

  const openFromPointer = (
    e: React.MouseEvent<HTMLElement>,
    rows: Row<T>[],
    requireSelected: boolean,
  ) => {
    if (!enabled || !e.currentTarget.contains(e.target as Node)) return
    const target = e.target as HTMLElement
    const td = target.closest<HTMLElement>('[data-grid-cell]')
    if (!td) return
    if (requireSelected && clickStartRef.current !== td) return
    const content = target.closest(INTERACTIVE_CONTENT)
    if (content && content !== td && td.contains(content)) return
    if (target.closest('[data-grid-cell-editor]')) return
    const rowId = td.closest('tr')?.getAttribute('data-grid-row')
    const row = rows.find((r) => r.id === rowId)
    const columnId = td.getAttribute('data-column-id')
    if (row && columnId) openCell(row, columnId)
  }

  // ─── The open editor ────────────────────────────────────────────────────
  const marksByRow = new Map<string, Record<string, CellMark>>()
  for (const entry of Object.values(entries)) {
    if (entry.phase === 'settled') continue
    const marks = marksByRow.get(entry.rowKey) ?? {}
    marks[entry.columnId] = { phase: entry.phase, error: entry.error }
    marksByRow.set(entry.rowKey, marks)
  }

  const sessionMark = session
    ? marksByRow.get(session.rowKey)?.[session.columnId]
    : undefined

  const editorElement = session ? (
    <CellEditorHost
      key={session.seq}
      session={session}
      status={sessionMark?.phase === 'error' ? 'error' : undefined}
      describedBy={
        sessionMark?.error
          ? messageId(session.rowKey, session.columnId)
          : undefined
      }
      onValueChange={(value) => {
        editorValueRef.current = value
      }}
      onPopupOpenChange={(open) => {
        popupOpenRef.current = open
      }}
      onCommitRequest={() => commitOpen({ next: null, focus: true })}
      onFocusOut={() => commitOpen({ next: null, focus: false })}
      getPopupHost={() => popupHostRef.current}
    />
  ) : null

  return {
    enabled,
    /** The rows with held values applied, for the table. */
    data: enabled ? applyEntries(data, entries, rowKeyOf, isTree) : data,
    /** The cell whose editor is open. */
    open: session
      ? { rowKey: session.rowKey, columnId: session.columnId }
      : null,
    editorElement,
    marksByRow,
    /** Prefix of the ids describing marked cells (see `cellMessageId`). */
    idPrefix,
    /** The latest failure, for the grid's live region. */
    announcement,
    rowKeyOf,
    isCellEditable,
    openCell,
    canCreateDraft,
    addDraft,
    onKeyDownCapture,
    onPointerDownCapture,
    onClick: (e: React.MouseEvent<HTMLElement>, rows: Row<T>[]) =>
      openFromPointer(e, rows, true),
    onDoubleClick: (e: React.MouseEvent<HTMLElement>, rows: Row<T>[]) =>
      openFromPointer(e, rows, false),
    pendingOpen,
    clearPendingOpen: () => setPendingOpen(null),
    pendingFocus,
    clearPendingFocus: () => setPendingFocus(null),
    /** Publishes the table for handlers that run outside render. */
    attachTable: (table: Table<T>) => {
      tableRef.current = table
    },
  }
}

export type GridCellEditingController<T extends RowData> = ReturnType<
  typeof useGridCellEditing<T>
>

/** Small controls one pixel under the body row's height, so opening an
 *  editor never makes its row taller. Text inputs size themselves from their
 *  font's line height, not `controlHeightSM`, so they take the cell's 12px. */
const EDITOR_THEME = {
  token: { controlHeightSM: 22 },
  components: {
    Input: { inputFontSizeSM: 12 },
    InputNumber: { inputFontSizeSM: 12 },
    DatePicker: { inputFontSizeSM: 12 },
    // Pads by (height − fontHeight) / 2; the default 22px fontHeight leaves
    // nothing to pad and the select renders 24px tall.
    Select: { fontSize: 12, fontHeight: 20 },
  },
} as const

/** Flex, so the inline-block control sits on no text line: a line box adds
 *  a descender gap under it that makes the row taller. */
const EDITOR_HOST_STYLE = { display: 'flex', width: '100%' } as const

interface CellEditorHostProps {
  session: EditSession
  status?: 'error'
  onValueChange: (value: unknown) => void
  onPopupOpenChange: (open: boolean) => void
  onCommitRequest: () => void
  onFocusOut: () => void
  /** The id of the cell's error message, while it has one. */
  describedBy?: string
  getPopupHost: () => HTMLElement | null
}

/**
 * Hosts the open editor inside its cell. Every antd popup the editor opens
 * mounts in the grid's popup host for this cell, which is how a click in a
 * dropdown counts as inside the cell without knowing any antd class names.
 */
function CellEditorHost({
  session,
  status,
  onValueChange,
  onPopupOpenChange,
  onCommitRequest,
  onFocusOut,
  describedBy,
  getPopupHost,
}: CellEditorHostProps) {
  const [value, setValue] = useState(session.initialValue)
  const ref = useRef<HTMLDivElement>(null)
  const mountedRef = useRef(false)

  useLayoutEffect(() => {
    if (mountedRef.current) return
    mountedRef.current = true
    const el = ref.current
    if (!el) return
    if (el.contains(document.activeElement)) return
    const control = el.querySelector<HTMLElement>(
      'input, textarea, select, button, [tabindex]:not([tabindex="-1"])',
    )
    control?.focus({ preventScroll: true })
    if (control instanceof HTMLInputElement) {
      try {
        if (session.initialInput !== undefined) {
          const end = control.value.length
          control.setSelectionRange(end, end)
        } else {
          control.select()
        }
      } catch {
        // Not every input type supports a selection.
      }
    }
  })

  return (
    <div
      ref={ref}
      data-grid-cell-editor=""
      style={EDITOR_HOST_STYLE}
      onBlur={(e) => {
        const next = e.relatedTarget as Node | null
        // No next element: the window lost focus, or the control remounted.
        if (!next) return
        if (e.currentTarget.closest('td')?.contains(next)) return
        if (getPopupHost()?.contains(next)) return
        onFocusOut()
      }}
    >
      <ConfigProvider
        getPopupContainer={() => getPopupHost() ?? document.body}
        theme={EDITOR_THEME}
      >
        {session.editor.render({
          value,
          onChange: (next: unknown) => {
            setValue(next)
            onValueChange(next)
          },
          initialInput: session.initialInput,
          commit: onCommitRequest,
          onPopupOpenChange,
          status,
          'aria-label': session.label,
          'aria-invalid': status === 'error' || undefined,
          'aria-describedby': describedBy,
          row: session.row,
        })}
      </ConfigProvider>
    </div>
  )
}
