import type { Editor } from '@tiptap/core'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { EditorState } from '@tiptap/pm/state'

/**
 * Replaces the editor's content with `markdown` without reporting an update, and starts a new undo
 * history. Steps recorded before a load would otherwise be mapped through the replace and undone
 * onto the new text, splicing the old content into it.
 *
 * @returns The document `markdown` parsed to.
 */
export const loadMarkdown = (
  editor: Editor,
  markdown: string,
): ProseMirrorNode => {
  editor.view.updateState(
    EditorState.create({
      doc: editor.state.doc,
      plugins: editor.state.plugins,
    }),
  )
  editor
    .chain()
    .setMeta('addToHistory', false)
    .setContent(markdown, { contentType: 'markdown', emitUpdate: false })
    .run()
  return editor.state.doc
}
