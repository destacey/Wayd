import type { ReactNode } from 'react'

/** What a {@link CellEditor.render} function is handed for the open cell. */
export interface CellEditorProps<E> {
  /** The editor's working value. */
  value: E
  onChange: (value: E) => void
  /**
   * The printable character that opened the cell, if typing opened it. A text
   * editor starts from it in place of the old value; others may ignore it.
   */
  initialInput?: string
  /** Asks the grid to commit the cell now and return focus to it, as a
   *  single-choice editor does once an option is picked. */
  commit: () => void
  /** Wire to the control's `onOpenChange`, so Escape closes the popup before
   *  it reverts the cell, and Enter in an open popup picks rather than commits. */
  onPopupOpenChange: (open: boolean) => void
  /** Set while the cell holds an error, so the control can show it. */
  status?: 'error'
  /** The column's label, for the control's accessible name. */
  'aria-label': string
  /** Set while the cell holds an error; pass to the control. */
  'aria-invalid'?: boolean
  /** The id of the cell's error message; pass to the control. */
  'aria-describedby'?: string
  /** The row being edited, for editors whose choices depend on it. */
  row: any
}

/** The fields shared by the members of a {@link pairedEditors} group. */
export interface EditorGroup {
  /** Identity of the group; the members share this object. */
  readonly id: object
  /**
   * Checks the members' values together before any of them is sent. Values
   * are keyed by role (`start`, `end`); return messages keyed the same way.
   */
  validate?: (
    values: Record<string, unknown>,
    row: any,
  ) => Partial<Record<string, string>> | undefined
}

/**
 * Makes a column's cells editable in place (`meta.editor`). The grid owns the
 * edit — opening, keys, the commit and its unsaved/saving/error state — and
 * calls the grid's `onCommit(rowId, { [field]: value })` with this one field.
 *
 * Values move through three shapes: the **model** value (`get`, what is sent,
 * e.g. `'2026-03-01'`), the **editor** value (`format`, what the control holds,
 * e.g. a Dayjs), and back (`parse`).
 */
export interface CellEditor<T = any, V = any, E = any> {
  /** The patch key, and the name server validation errors use. A function
   *  when it differs by row (a milestone's single date). */
  field: string | ((row: T) => string)
  /** The current server value of the field, in model form. */
  get: (row: T) => V
  /**
   * A copy of the row with the value applied — how a pending or failed value
   * is shown before the server confirms it, in the cell and anything else
   * drawn from the row (a Gantt bar).
   */
  apply: (row: T, value: V) => T
  /** Model value → editor value. Identity by default. */
  format?: (value: V, row: T) => E
  /** Editor value → model value. Identity by default. */
  parse?: (value: E, row: T) => V
  /**
   * The editor value when typing a character opened the cell — typing
   * replaces the value. Without it the editor opens on the current value.
   */
  fromInput?: (text: string, row: T) => E
  /** A message when the value cannot be sent. */
  validate?: (value: V, row: T) => string | undefined
  /** Whether this row's cell is editable. Every row by default. */
  editable?: (row: T, context: { isDraft: boolean }) => boolean
  /**
   * A draft is not created until this cell holds a value too, as well as its
   * Name. Only cells editable on the draft count.
   */
  requiredToCreate?: boolean
  /** Whether two model values are the same. `Object.is`, or element-wise for
   *  arrays, by default. */
  equals?: (a: V, b: V) => boolean
  render: (props: CellEditorProps<E>) => ReactNode
  /** Set by {@link pairedEditors}: the linked fields this one commits with. */
  group?: EditorGroup
  /** This editor's role within its group (`start`, `end`). */
  role?: string
}

/** Where a cell stands between the user's edit and the server. */
export type CellEditPhase = 'unsaved' | 'saving' | 'settled' | 'error'

/** A cell's edit state, as the body marks it. */
export interface CellMark {
  phase: Exclude<CellEditPhase, 'settled'>
  /** The error message, already prefixed with the column label. */
  error?: string
}
