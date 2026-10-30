import { type Editor, Extension } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { toStoredMarkdown } from './stored-markdown'

/** Mutable settings of `MarkdownLengthLimit`, read on each transaction. */
export interface MarkdownLengthLimitStorage {
  /** Limit on the serialized markdown; `undefined` for none. */
  maxLength: number | undefined
}

declare module '@tiptap/core' {
  interface Storage {
    markdownLengthLimit: MarkdownLengthLimitStorage
  }
}

/**
 * Refuses an edit that would make the serialized markdown longer than `storage.maxLength`, which is
 * what the API validates; the visible text is shorter than the markdown it saves. An edit that
 * shortens the text is always allowed, so content loaded over the limit can still be cut down.
 * Loading content (`setContent` without an update) is never refused.
 */
export const MarkdownLengthLimit = Extension.create<
  object,
  MarkdownLengthLimitStorage
>({
  name: 'markdownLengthLimit',

  addStorage() {
    return { maxLength: undefined }
  },

  addProseMirrorPlugins() {
    const { editor, storage } = this
    return [
      new Plugin({
        key: new PluginKey('markdownLengthLimit'),
        filterTransaction: (transaction, state) => {
          const { maxLength } = storage
          const manager = editor.markdown
          if (
            !maxLength ||
            !manager ||
            !transaction.docChanged ||
            transaction.getMeta('preventUpdate')
          ) {
            return true
          }

          const nextLength = toStoredMarkdown(
            manager.serialize(transaction.doc.toJSON()),
          ).length
          if (nextLength <= maxLength) return true
          return (
            nextLength <
            toStoredMarkdown(manager.serialize(state.doc.toJSON())).length
          )
        },
      }),
    ]
  },
})

/** Sets the limit `MarkdownLengthLimit` enforces on `editor`; takes effect on the next edit. */
export const setMarkdownMaxLength = (
  editor: Editor,
  maxLength: number | undefined,
): void => {
  editor.storage.markdownLengthLimit.maxLength = maxLength
}
