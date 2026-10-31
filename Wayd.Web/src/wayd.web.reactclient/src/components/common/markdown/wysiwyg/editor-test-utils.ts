import { Editor } from '@tiptap/core'
import {
  markdownEditorExtensions,
  markdownEditorPasteRules,
} from './markdown-extensions'

/**
 * Stubs the layout calls ProseMirror makes to scroll the selection into view, which jsdom does
 * not implement. Call once per test file, in `beforeAll`.
 */
export const stubEditorLayout = () => {
  Range.prototype.getClientRects = () =>
    ({
      length: 0,
      item: () => null,
      [Symbol.iterator]: [][Symbol.iterator],
    }) as unknown as DOMRectList
  Range.prototype.getBoundingClientRect = () => new DOMRect(0, 0, 0, 0)
  document.elementFromPoint = () => null
}

/** A mounted editor with the shipped extensions, holding `markdown`. Destroy it after the test. */
export const createTestEditor = (markdown: string): Editor => {
  const element = document.createElement('div')
  document.body.appendChild(element)
  return new Editor({
    element,
    extensions: markdownEditorExtensions(),
    enablePasteRules: markdownEditorPasteRules,
    content: markdown,
    contentType: 'markdown',
  })
}

/** Selects the first occurrence of `text` in the editor's document. */
export const selectText = (editor: Editor, text: string) => {
  let from = -1
  editor.state.doc.descendants((node, position) => {
    if (from >= 0 || !node.isText) return from < 0
    const index = node.text!.indexOf(text)
    if (index >= 0) from = position + index
    return false
  })
  if (from < 0) throw new Error(`"${text}" is not in the document`)
  editor.commands.setTextSelection({ from, to: from + text.length })
}

/** Types `text` one character at a time where the cursor is, running input rules as typing does. */
export const typeText = (editor: Editor, text: string) => {
  for (const character of text) {
    const { view } = editor
    const { from, to } = view.state.selection
    const handled = view.someProp('handleTextInput', (handler) =>
      handler(view, from, to, character, () => view.state.tr),
    )
    if (!handled) view.dispatch(view.state.tr.insertText(character, from, to))
  }
}

/** Puts the cursor just before the first occurrence of `text`. */
export const placeCursorBefore = (editor: Editor, text: string) => {
  selectText(editor, text)
  editor.commands.setTextSelection(editor.state.selection.from)
}

/**
 * Presses `key` in the editor as a keydown does. Tiptap's `keyboardShortcut` command replays only
 * the steps a handler made, not the selection it set.
 */
export const pressKey = (editor: Editor, key: string) => {
  const { view } = editor
  view.someProp('handleKeyDown', (handler) =>
    handler(view, new KeyboardEvent('keydown', { key })),
  )
}
