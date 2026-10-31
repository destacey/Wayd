import { Extension, type JSONContent } from '@tiptap/core'
import { Slice } from '@tiptap/pm/model'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { isSafeImageSrc, isSafeLinkHref } from './safe-urls'

/** A line that starts a heading, list, quote, fence or table row. */
const BLOCK_SYNTAX =
  /^ {0,3}(?:#{1,6}\s|[-*+]\s|\d{1,9}[.)]\s|>|```|~~~|\|.*\|\s*$)/m

/**
 * Inline syntax unlikely in prose: `**bold**`, `__bold__`, `~~strike~~`, `` `code` `` or a
 * `[link](address)`. A single `*` or `_` is left out, since `2 * 3 * 4` and `snake_case` are prose.
 */
const INLINE_SYNTAX =
  /\*\*[^*\n]+\*\*|__[^_\n]+__|~~[^~\n]+~~|`[^`\n]+`|\[[^\]\n]+\]\([^)\s]+\)/

/** Whether pasted plain text reads as markdown rather than prose. */
export const looksLikeMarkdown = (text: string): boolean =>
  BLOCK_SYNTAX.test(text) || INLINE_SYNTAX.test(text)

/**
 * `content` without the links and images the sanitizer would refuse: an unsafe link keeps its
 * text, and an unsafe image is left out, as pasted HTML is treated.
 */
export const withSafeUrls = (content: JSONContent): JSONContent => ({
  ...content,
  marks: content.marks?.filter(
    (mark) => mark.type !== 'link' || isSafeLinkHref(mark.attrs?.href),
  ),
  content: content.content
    ?.filter(
      (child) => child.type !== 'image' || isSafeImageSrc(child.attrs?.src),
    )
    .map(withSafeUrls),
})

/**
 * Formats plain text pasted as markdown — from an AI chat, a README or another field's source
 * view — instead of inserting its syntax as text, which would be stored escaped. Text that does
 * not look like markdown, a paste with HTML (Word, Outlook, web pages), a paste into a code block
 * and a paste without formatting (Ctrl+Shift+V) keep the editor's own handling.
 */
export const MarkdownPaste = Extension.create({
  name: 'markdownPaste',

  addProseMirrorPlugins() {
    const { editor } = this
    return [
      new Plugin({
        key: new PluginKey('markdownPaste'),
        props: {
          // ProseMirror asks this only for text pasted without HTML, and not inside code.
          clipboardTextParser: (text, _context, plain) => {
            const markdown = text.replace(/\r\n?/g, '\n')
            if (plain || !editor.markdown || !looksLikeMarkdown(markdown)) {
              // Falls back to ProseMirror's own plain-text handling.
              return null as unknown as Slice
            }
            const doc = editor.schema.nodeFromJSON(
              withSafeUrls(editor.markdown.parse(markdown)),
            )
            return Slice.maxOpen(doc.content)
          },
        },
      }),
    ]
  },
})
