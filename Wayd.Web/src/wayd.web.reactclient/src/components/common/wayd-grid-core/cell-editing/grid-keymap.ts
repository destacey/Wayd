/**
 * The keys a WaydGrid answers to, as the help panel lists them. It lives
 * beside the handlers (`use-grid-cell-navigation.ts`, `use-grid-cell-editing.tsx`)
 * and is the only list of them: change a key there and here together, and
 * every grid's help follows.
 */
export interface GridKeyBinding {
  /** The key or gesture, as the help panel names it. */
  keys: string
  /** What it does. */
  description: string
  /** The row action the grid must support for the key to be listed. */
  requires?: GridRowActionKind
}

/** The row actions a grid can answer to from the keyboard. */
export type GridRowActionKind = 'insert' | 'move' | 'delete' | 'open'

/** Which row actions a grid supports, for the help it lists. */
export type GridRowActionSupport = Partial<Record<GridRowActionKind, boolean>>

export interface GridKeymapSection {
  title: string
  /** `editing` sections apply only to a grid with editable columns;
   *  `tree` only to a tree grid; `rows` lists each key whose row action the
   *  grid supports. */
  appliesTo: 'always' | 'editing' | 'tree' | 'rows'
  bindings: GridKeyBinding[]
}

export const GRID_KEYMAP: readonly GridKeymapSection[] = [
  {
    title: 'Moving Around',
    appliesTo: 'always',
    bindings: [
      { keys: 'Arrow keys', description: 'Move between cells' },
      { keys: 'Home / End', description: 'First or last cell in the row' },
      {
        keys: 'Ctrl + Home / End',
        description: 'First or last cell in the grid',
      },
      { keys: 'Page Up / Down', description: 'Move a screen of rows' },
      {
        keys: 'Enter / Space',
        description: 'Open the row, or expand or collapse a group heading',
      },
      { keys: 'Shift + F10', description: "Open the row's actions menu" },
      { keys: 'Tab', description: 'Leave the grid' },
    ],
  },
  {
    title: 'Tree',
    appliesTo: 'tree',
    bindings: [
      {
        keys: 'Right / Left in the tree column',
        description: 'Expand or collapse; Left on a child moves to its parent',
      },
    ],
  },
  {
    title: 'Editing',
    appliesTo: 'editing',
    bindings: [
      {
        keys: 'Enter, F2, double-click, or click the selected cell',
        description: 'Edit the cell',
      },
      {
        keys: 'Type a character',
        description:
          'Edit the cell; in a text or number cell the character replaces the value',
      },
      { keys: 'Down arrow in a date cell', description: 'Open the calendar' },
      {
        keys: 'Enter',
        description: 'Save and move down (in an open list, pick the option)',
      },
      {
        keys: 'Tab / Shift + Tab',
        description:
          'Save and edit the next or previous editable cell; past the last, leave the grid',
      },
      {
        keys: 'Esc',
        description: 'Close an open list or calendar, then undo the edit',
      },
      {
        keys: 'Esc on an unsaved or failed cell',
        description: 'Discard its change',
      },
      { keys: 'Click elsewhere', description: 'Save the cell' },
    ],
  },
  {
    title: 'Rows',
    appliesTo: 'rows',
    bindings: [
      {
        keys: 'Insert / Ctrl + Enter',
        description:
          'Add a row below (Ctrl + Enter where Insert is missing or a screen reader takes it)',
        requires: 'insert',
      },
      {
        keys: 'Alt + Shift + Right / Left',
        description:
          'Indent under the row above at the same level, or outdent to after the parent',
        requires: 'move',
      },
      {
        keys: 'Ctrl + Delete',
        description: 'Delete the row',
        requires: 'delete',
      },
      {
        keys: 'Ctrl + Shift + Enter',
        description: 'Open the row’s details',
        requires: 'open',
      },
    ],
  },
]

/** The sections that apply to a grid, each holding only the keys it
 *  supports; a section left with no keys is dropped. */
export const gridKeymapFor = (options: {
  editing: boolean
  tree: boolean
  rows?: GridRowActionSupport
}): GridKeymapSection[] =>
  GRID_KEYMAP.filter(
    (section) =>
      section.appliesTo === 'always' ||
      section.appliesTo === 'rows' ||
      (section.appliesTo === 'editing' && options.editing) ||
      (section.appliesTo === 'tree' && options.tree),
  )
    .map((section) => ({
      ...section,
      bindings: section.bindings.filter(
        (binding) => !binding.requires || !!options.rows?.[binding.requires],
      ),
    }))
    .filter((section) => section.bindings.length > 0)

/**
 * What the key hints below the grid describe: the selected cell, or the
 * open editor by its kind (a custom editor reads as `text`).
 */
export type GridHintMode = 'cell' | 'text' | 'select' | 'multiselect' | 'date'

/** One key in the hint strip: the key and a word or two of what it does. */
export interface GridKeyHint {
  keys: string
  label: string
}

interface GridHintDefinition extends GridKeyHint {
  modes: readonly GridHintMode[]
  /** Shown only on a cell that edits. */
  editable?: boolean
  requires?: GridRowActionKind
}

const SAVE_AND_DOWN = { keys: 'Enter', label: 'save & down' }
const SAVE_AND_NEXT = { keys: 'Tab', label: 'save & next' }

/**
 * The short form of the keymap the hint strip shows, kept beside
 * {@link GRID_KEYMAP} so a key changed in one is changed in the other.
 */
const GRID_HINTS: readonly GridHintDefinition[] = [
  { keys: 'Enter', label: 'edit', modes: ['cell'], editable: true },
  { keys: 'Arrows', label: 'move', modes: ['cell'] },
  { keys: 'Insert', label: 'add row', modes: ['cell'], requires: 'insert' },
  {
    keys: 'Alt + Shift + ← / →',
    label: 'outdent / indent',
    modes: ['cell'],
    requires: 'move',
  },
  {
    keys: 'Ctrl + Delete',
    label: 'delete',
    modes: ['cell'],
    requires: 'delete',
  },
  {
    keys: 'Ctrl + Shift + Enter',
    label: 'open',
    modes: ['cell'],
    requires: 'open',
  },
  { keys: 'Tab', label: 'leave grid', modes: ['cell'] },
  { keys: '↓', label: 'calendar', modes: ['date'] },
  { keys: '↑ / ↓', label: 'choose', modes: ['select', 'multiselect'] },
  { keys: 'Enter', label: 'pick', modes: ['select'] },
  { keys: 'Enter', label: 'add / remove', modes: ['multiselect'] },
  { ...SAVE_AND_DOWN, modes: ['text', 'date'] },
  { keys: 'Tab', label: 'next cell', modes: ['select'] },
  { ...SAVE_AND_NEXT, modes: ['text', 'date', 'multiselect'] },
  { keys: 'Esc', label: 'undo', modes: ['text', 'date'] },
  { keys: 'Esc', label: 'close list', modes: ['select', 'multiselect'] },
]

/** The keys the hint strip shows for what the user is doing now. */
export const gridHintsFor = (options: {
  mode: GridHintMode
  /** The selected cell edits (mode `cell`). */
  cellEditable?: boolean
  rows?: GridRowActionSupport
}): GridKeyHint[] =>
  GRID_HINTS.filter(
    (hint) =>
      hint.modes.includes(options.mode) &&
      (!hint.editable || !!options.cellEditable) &&
      (!hint.requires || !!options.rows?.[hint.requires]),
  ).map(({ keys, label }) => ({ keys, label }))
