import { InputRule } from '@tiptap/core'
import { inputRegex as imageInputRegex } from '@tiptap/extension-image'
import { isSafeImageSrc } from './safe-urls'

/**
 * Turns a typed `![alt](address)` into an image as its `)` is typed, when the address is one the
 * sanitizer keeps. Replaces Tiptap's own rule, which inserts the closing `)` a second time because
 * the typed character is already in the document when input rules run.
 */
export const markdownImageInputRule = () =>
  new InputRule({
    find: imageInputRegex,
    handler: ({ state, range, match }) => {
      const [whole, image, alt, src, title] = match
      if (!isSafeImageSrc(src)) return null

      const start = range.from + whole.length - image.length
      state.tr.replaceWith(
        start,
        range.to,
        state.schema.nodes.image.create({ src, alt, title }),
      )
    },
  })
