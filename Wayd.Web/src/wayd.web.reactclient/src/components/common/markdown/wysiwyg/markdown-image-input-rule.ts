import { InputRule } from '@tiptap/core'
import { inputRegex as imageInputRegex } from '@tiptap/extension-image'
import { isAttachmentPath } from '../attachment-path'
import { isSafeImageSrc } from './safe-urls'

/**
 * Turns a typed `![alt](address)` into an image as its `)` is typed, when the address is one the
 * sanitizer keeps; an uploaded image's address only in a field that takes uploads. Replaces
 * Tiptap's own rule, which inserts the closing `)` a second time because the typed character is
 * already in the document when input rules run.
 */
export const markdownImageInputRule = (acceptsAttachments: () => boolean) =>
  new InputRule({
    find: imageInputRegex,
    handler: ({ state, range, match }) => {
      const [whole, image, alt, src, title] = match
      if (!isSafeImageSrc(src)) return null
      if (isAttachmentPath(src) && !acceptsAttachments()) return null

      const start = range.from + whole.length - image.length
      state.tr.replaceWith(
        start,
        range.to,
        state.schema.nodes.image.create({ src, alt, title }),
      )
    },
  })
