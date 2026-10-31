import { InputRule } from '@tiptap/core'
import { inputRegex as taskItemInputRegex } from '@tiptap/extension-list'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { TextSelection } from '@tiptap/pm/state'

/**
 * Turns a bullet into a task when `[ ] ` or `[x] ` is typed at its start, so typing the markdown
 * `- [ ] ` gives a task. Tiptap's own task rule wraps the line in a task list, which a bullet's
 * first paragraph cannot hold, so without this the brackets stay as text and are stored escaped.
 *
 * Only that item becomes a task: the bullets before and after it stay bullet lists, as markdown
 * reads a list that mixes the two.
 */
export const bulletToTaskInputRule = () =>
  new InputRule({
    find: taskItemInputRegex,
    handler: ({ state, range, match }) => {
      const $from = state.doc.resolve(range.from)
      const depth = $from.depth
      if (depth < 2) return null

      const item = $from.node(depth - 1)
      const list = $from.node(depth - 2)
      if (
        item.type.name !== 'listItem' ||
        list.type.name !== 'bulletList' ||
        $from.index(depth - 1) !== 0
      ) {
        return null
      }

      const { schema, tr } = state
      const listPos = $from.before(depth - 2)
      const itemIndex = $from.index(depth - 2)

      tr.delete(range.from, range.to)
      const updatedList = tr.doc.nodeAt(listPos)!
      const updatedItem = updatedList.child(itemIndex)

      const before: ProseMirrorNode[] = []
      const after: ProseMirrorNode[] = []
      updatedList.forEach((child, _offset, index) => {
        if (index < itemIndex) before.push(child)
        else if (index > itemIndex) after.push(child)
      })

      const taskList = schema.nodes.taskList.create(
        null,
        schema.nodes.taskItem.create(
          { checked: match[2] === 'x' },
          updatedItem.content,
        ),
      )
      const parts = [
        ...(before.length
          ? [updatedList.type.create(updatedList.attrs, before)]
          : []),
        taskList,
        ...(after.length
          ? [updatedList.type.create(updatedList.attrs, after)]
          : []),
      ]
      tr.replaceWith(listPos, listPos + updatedList.nodeSize, parts)

      // Into the task list, the task item and its paragraph.
      const taskListPos = listPos + (before.length ? parts[0].nodeSize : 0)
      tr.setSelection(TextSelection.create(tr.doc, taskListPos + 3))
    },
  })
