import { InputRule } from '@tiptap/core'
import { toLinkHref } from './safe-urls'

/**
 * `[text](address)` just closed, not preceded by `!` (an image) or `\` (escaped). Group 1 is the
 * whole link, group 2 its text and group 3 its address.
 */
const TYPED_LINK = /(?:^|[^!\\])(\[([^[\]\n]+)\]\(([^()\s]+)\))$/

/**
 * Turns `[text](address)` into a link as its `)` is typed, so the markdown habit gives a link
 * rather than brackets stored escaped. The address is read as the link dialog reads it: a bare
 * host gets `https://`, and an address the sanitizer would refuse leaves the text as typed.
 */
export const markdownLinkInputRule = () =>
  new InputRule({
    find: TYPED_LINK,
    handler: ({ state, range, match }) => {
      const [whole, link, text, address] = match
      const href = toLinkHref(address)
      if (!href) return null

      const { schema, tr } = state
      const linkType = schema.marks.link
      const start = range.from + whole.length - link.length
      const marks = state.doc.resolve(start).marks()
      tr.replaceWith(
        start,
        range.to,
        schema.text(text, [...marks, linkType.create({ href })]),
      )
      // Text typed next continues outside the link.
      tr.removeStoredMark(linkType)
    },
  })
