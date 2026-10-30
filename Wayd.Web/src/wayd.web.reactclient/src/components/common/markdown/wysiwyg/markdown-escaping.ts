import type { JSONContent } from '@tiptap/core'
import { MarkdownManager } from '@tiptap/markdown'

type EncodeText = (
  text: string,
  node: JSONContent,
  parentNode?: JSONContent,
) => string

interface PatchableManager {
  encodeTextForMarkdown?: EncodeText
  __waydMinimalEscaping?: true
}

/** `&amp;` that would not start an entity if written as `&`. */
const NEEDLESS_AMP = /&amp;(?!#?[a-z0-9]+;)/gi
/** `&lt;` that would not start a tag, comment or autolink if written as `<`. */
const NEEDLESS_LT = /&lt;(?![a-z/!?])/gi
/**
 * `&gt;` anywhere but the start of a line (after any indentation), where `>` would open a quote.
 * A text node may start a line, after a hard break or at the start of a block. Only applied when no
 * `&lt;` was kept, so text written as `&lt;tag&gt;` keeps both halves.
 */
const NEEDLESS_GT = /(?<!(?:^|\n)[ \t]*)&gt;/g
/** `\_` between letters or digits: GFM never reads an intraword `_` as emphasis. */
const NEEDLESS_UNDERSCORE = /(?<=[\p{L}\p{N}])\\_(?=[\p{L}\p{N}])/gu

/**
 * Narrows the escaping `@tiptap/markdown` applies to text, so saving after an edit does not
 * rewrite characters the user never touched: `R&D` stays `R&D`, not `R&amp;D`, and `snake_case`
 * stays unescaped. Escapes that change meaning (`&copy;` typed as text, `<b>` typed as text, a
 * leading `>`) are kept.
 *
 * Tiptap applies its escaping in `MarkdownManager.encodeTextForMarkdown`, a private method with no
 * option, so this wraps it on the prototype. The Tiptap version is pinned and the round-trip
 * harness fails if an upgrade renames or changes it.
 */
export const installMinimalMarkdownEscaping = (): void => {
  const prototype = MarkdownManager.prototype as unknown as PatchableManager
  const encode = prototype.encodeTextForMarkdown
  if (prototype.__waydMinimalEscaping || typeof encode !== 'function') return

  prototype.encodeTextForMarkdown = function (text, node, parentNode) {
    const encoded = encode.call(this, text, node, parentNode)
    // Text inside code comes back untouched and must stay that way.
    if (encoded === text) return encoded
    const narrowed = encoded
      .replace(NEEDLESS_AMP, '&')
      .replace(NEEDLESS_LT, '<')
      .replace(NEEDLESS_UNDERSCORE, '_')
    return narrowed.includes('&lt;')
      ? narrowed
      : narrowed.replace(NEEDLESS_GT, '>')
  }
  prototype.__waydMinimalEscaping = true
}
