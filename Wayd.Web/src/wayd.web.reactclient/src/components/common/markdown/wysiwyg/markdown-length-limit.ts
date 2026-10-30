import { type Editor, Extension, type JSONContent } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { toStoredMarkdown } from './stored-markdown'

/** Mutable settings of `MarkdownLengthLimit`, read on each transaction. */
export interface MarkdownLengthLimitStorage {
  /** Limit on the stored markdown; `undefined` for none. */
  maxLength: number | undefined
  /** Told the length an edit would have reached each time one is refused. */
  onRefuse: ((length: number) => void) | undefined
}

declare module '@tiptap/core' {
  interface Storage {
    markdownLengthLimit: MarkdownLengthLimitStorage
  }
}

/**
 * Refuses an edit that would make the stored markdown longer than `storage.maxLength`, which is
 * what the API validates; the visible text is shorter than the markdown it saves. The whole edit is
 * refused, so a paste that would cross the limit inserts nothing. An edit that shortens the text is
 * always allowed, so content loaded over the limit can still be cut down. Loading content
 * (`setContent` without an update) is never refused.
 */
export const MarkdownLengthLimit = Extension.create<
  object,
  MarkdownLengthLimitStorage
>({
  name: 'markdownLengthLimit',

  addStorage() {
    return { maxLength: undefined, onRefuse: undefined }
  },

  addProseMirrorPlugins() {
    const { editor, storage } = this
    const storedLength = (doc: JSONContent) =>
      toStoredMarkdown(editor.markdown!.serialize(doc)).length
    return [
      new Plugin({
        key: new PluginKey('markdownLengthLimit'),
        filterTransaction: (transaction, state) => {
          const { maxLength } = storage
          if (
            !maxLength ||
            !editor.markdown ||
            !transaction.docChanged ||
            transaction.getMeta('preventUpdate')
          ) {
            return true
          }

          const nextLength = storedLength(transaction.doc.toJSON())
          if (nextLength <= maxLength) return true
          if (nextLength < storedLength(state.doc.toJSON())) return true
          storage.onRefuse?.(nextLength)
          return false
        },
      }),
    ]
  },
})

/** Sets the limit `MarkdownLengthLimit` enforces on `editor`, and who to tell when it refuses. */
export const setMarkdownLengthLimit = (
  editor: Editor,
  maxLength: number | undefined,
  onRefuse?: (length: number) => void,
): void => {
  editor.storage.markdownLengthLimit.maxLength = maxLength
  editor.storage.markdownLengthLimit.onRefuse = onRefuse
}

/**
 * Undoes an input the length limit refused in a controlled `textarea`: puts back `previous` and
 * leaves the caret where the refused input began. Left to React, the restored value would move the
 * caret to the end.
 */
export const restoreRefusedInput = (
  textarea: HTMLTextAreaElement,
  previous: string,
): void => {
  const inserted = textarea.value.length - previous.length
  const caret = Math.max(0, textarea.selectionStart - inserted)
  textarea.value = previous
  textarea.setSelectionRange(caret, caret)
}
