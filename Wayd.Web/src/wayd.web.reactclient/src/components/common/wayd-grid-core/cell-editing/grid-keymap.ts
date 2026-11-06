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
}

export interface GridKeymapSection {
  title: string
  /** `editing` sections apply only to a grid with editable columns;
   *  `tree` only to a tree grid. */
  appliesTo: 'always' | 'editing' | 'tree'
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
]

/** The sections that apply to a grid. */
export const gridKeymapFor = (options: { editing: boolean; tree: boolean }) =>
  GRID_KEYMAP.filter(
    (section) =>
      section.appliesTo === 'always' ||
      (section.appliesTo === 'editing' && options.editing) ||
      (section.appliesTo === 'tree' && options.tree),
  )
