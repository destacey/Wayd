import { Extension } from '@tiptap/core'
import type { Node as ProseMirrorNode, Schema } from '@tiptap/pm/model'
import { TextSelection } from '@tiptap/pm/state'

/**
 * The cells of a markdown table row such as `| Name | Owner |`, or `null` when `text` is not one.
 * At least two cells are needed, so a line that only happens to start and end with `|` stays text.
 * An escaped `\|` stays inside its cell.
 */
export const parseTableHeaderRow = (text: string): string[] | null => {
  const trimmed = text.trim()
  if (trimmed.length < 3 || !trimmed.startsWith('|') || !trimmed.endsWith('|'))
    return null
  if (trimmed.endsWith('\\|')) return null
  const cells = trimmed
    .slice(1, -1)
    .split(/(?<!\\)\|/)
    .map((cell) => cell.trim().replace(/\\\|/g, '|'))
  return cells.length >= 2 ? cells : null
}

const cellParagraph = (schema: Schema, text: string) =>
  schema.nodes.paragraph.create(null, text ? schema.text(text) : null)

/** A table whose header row holds `headers`, with one empty row below it. */
const createTable = (schema: Schema, headers: string[]): ProseMirrorNode => {
  const { table, tableRow, tableHeader, tableCell } = schema.nodes
  return table.create(null, [
    tableRow.create(
      null,
      headers.map((header) =>
        tableHeader.create(null, cellParagraph(schema, header)),
      ),
    ),
    tableRow.create(
      null,
      headers.map(() => tableCell.create(null, cellParagraph(schema, ''))),
    ),
  ])
}

/**
 * Enter at the end of a line typed as a markdown header row (`| Name | Owner |`) turns the line
 * into a table with those headers and an empty row, with the cursor in its first cell. Lines in a
 * table, in code or in a list item's own first line keep Enter's usual behavior.
 */
export const MarkdownTableShortcut = Extension.create({
  name: 'markdownTableShortcut',

  // Ahead of the list and paragraph Enter handlers.
  priority: 150,

  addKeyboardShortcuts() {
    return {
      Enter: ({ editor }) => {
        const { state } = editor
        const { selection, schema } = state
        const { $from, empty } = selection
        const line = $from.parent
        if (
          !empty ||
          line.type !== schema.nodes.paragraph ||
          $from.parentOffset !== line.content.size
        ) {
          return false
        }
        for (let depth = $from.depth - 1; depth > 0; depth--) {
          if ($from.node(depth).type.spec.tableRole) return false
        }

        const headers = parseTableHeaderRow(line.textContent)
        if (!headers) return false

        const container = $from.node(-1)
        const index = $from.index(-1)
        if (!container.canReplaceWith(index, index + 1, schema.nodes.table)) {
          return false
        }

        const table = createTable(schema, headers)
        const tablePos = $from.before()
        return editor.commands.command(({ tr }) => {
          tr.replaceWith(tablePos, $from.after(), table)
          // Into the table, past the header row, into the first row, its cell and its paragraph.
          const firstCell = tablePos + 1 + table.child(0).nodeSize + 3
          tr.setSelection(TextSelection.create(tr.doc, firstCell))
          tr.scrollIntoView()
          return true
        })
      },
    }
  },
})
